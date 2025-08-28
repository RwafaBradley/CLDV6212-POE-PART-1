using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ABCRetailers.Models;    
using ABCRetailers.Models;               
using ABCRetailers.Models.ViewModels;
using ABCRetailers.Services;  
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ABCRetailers.Controllers
{
    
    public class OrderController : Controller
    {
        private readonly IAzureStorageService _storageService;
        private readonly ILogger<OrderController> _logger;

        private const string OrderPartition = "Order";
        private const string CustomerPartition = "Customer";
        private const string ProductPartition = "Product";

        public OrderController(IAzureStorageService storageService, ILogger<OrderController> logger)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

       
        public async Task<IActionResult> Index()
        {
            try
            {
                var orders = await _storageService.GetAllEntitiesAsync<Order>();
                return View(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading orders: {Message}", ex.Message);
                TempData["Error"] = $"Error loading orders: {ex.Message}";
                return View(Enumerable.Empty<Order>());
            }
        }

        
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest();

            try
            {
                var order = await _storageService.GetEntityAsync<Order>(OrderPartition, id);
                if (order == null)
                    return NotFound();

                return View(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading order details: {Message}", ex.Message);
                TempData["Error"] = $"Error loading order: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        
        public async Task<IActionResult> Create()
        {
            var model = new OrderCreateViewModel();
            await PopulateDropdowns(model);
            return View(model);
        }

       
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrderCreateViewModel model)
        {
            _logger.LogInformation("ModelState valid: {IsValid}", ModelState.IsValid);
            if (!ModelState.IsValid)
            {
                foreach (var kv in ModelState)
                    foreach (var error in kv.Value.Errors)
                        _logger.LogWarning("Model error for {Key}: {Error}", kv.Key, error.ErrorMessage);

                await PopulateDropdowns(model);
                return View(model);
            }

            try
            {
                var customer = await _storageService.GetEntityAsync<Customer>(CustomerPartition, model.CustomerId);
                var product = await _storageService.GetEntityAsync<Product>(ProductPartition, model.ProductId);

                if (customer == null || product == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid customer or product selected.");
                    await PopulateDropdowns(model);
                    return View(model);
                }

                if (product.StockAvailable < model.Quantity)
                {
                    ModelState.AddModelError("Quantity", $"Insufficient stock. Available: {product.StockAvailable}");
                    await PopulateDropdowns(model);
                    return View(model);
                }

               
                var orderDate = model.OrderDate == default
                    ? DateTime.UtcNow
                    : DateTime.SpecifyKind(model.OrderDate, DateTimeKind.Utc);

                var order = new Order
                {
                    CustomerId = model.CustomerId,
                    Username = customer.Username,
                    ProductId = model.ProductId,
                    ProductName = product.ProductName,
                    OrderDate = orderDate,   
                    Quantity = model.Quantity,
                    UnitPrice = product.Price,
                    TotalPrice = product.Price * model.Quantity,
                    Status = model.Status ?? "Submitted",

                    PartitionKey = OrderPartition,
                    RowKey = string.IsNullOrWhiteSpace(model.Id) ? Guid.NewGuid().ToString() : model.Id
                };

                
                await _storageService.AddEntityAsync(order);

               
                product.StockAvailable -= model.Quantity;
                await _storageService.UpdateEntityAsync(product);

               
                var orderMessage = JsonSerializer.Serialize(new
                {
                    OrderId = order.RowKey,
                    order.CustomerId,
                    order.Username,
                    order.ProductId,
                    order.ProductName,
                    order.Quantity,
                    order.TotalPrice,
                    order.Status,
                    Action = "Created",
                    Timestamp = DateTime.UtcNow
                });
                await _storageService.SendMessageAsync("order-notifications", orderMessage);

                
                var stockMessage = JsonSerializer.Serialize(new
                {
                    ProductId = product.RowKey,
                    product.ProductName,
                    product.StockAvailable,
                    Action = "StockUpdated",
                    Timestamp = DateTime.UtcNow
                });
                await _storageService.SendMessageAsync("stock-updates", stockMessage);

                TempData["Success"] = $"Order for '{order.ProductName}' by '{order.Username}' created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order: {Message}", ex.Message);
                ModelState.AddModelError(string.Empty, $"Error creating order: {ex.Message}");
                await PopulateDropdowns(model);
                return View(model);
            }
        }


        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest();

            try
            {
                var order = await _storageService.GetEntityAsync<Order>(OrderPartition, id);
                if (order == null)
                    return NotFound();

                var model = new OrderCreateViewModel
                {
                    Id = order.Id,
                    CustomerId = order.CustomerId,
                    ProductId = order.ProductId,
                    OrderDate = order.OrderDate,
                    Quantity = order.Quantity
                   
                };

                await PopulateDropdowns(model);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading order for edit: {Message}", ex.Message);
                TempData["Error"] = $"Error loading order: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(OrderCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(model);
                return View(model);
            }

            try
            {
                
                if (string.IsNullOrWhiteSpace(model.Id))
                {
                    ModelState.AddModelError(string.Empty, "Order Id is required.");
                    await PopulateDropdowns(model);
                    return View(model);
                }

                var existingOrder = await _storageService.GetEntityAsync<Order>(OrderPartition, model.Id);
                if (existingOrder == null)
                {
                    ModelState.AddModelError(string.Empty, "Order not found.");
                    await PopulateDropdowns(model);
                    return View(model);
                }

                var product = await _storageService.GetEntityAsync<Product>(ProductPartition, model.ProductId);
                if (product == null)
                {
                    ModelState.AddModelError(string.Empty, "Selected product not found.");
                    await PopulateDropdowns(model);
                    return View(model);
                }

                if (model.Quantity != existingOrder.Quantity)
                {
                    var delta = model.Quantity - existingOrder.Quantity;
                    if (delta > 0 && product.StockAvailable < delta)
                    {
                        ModelState.AddModelError("Quantity", $"Insufficient stock for the requested increase. Available: {product.StockAvailable}");
                        await PopulateDropdowns(model);
                        return View(model);
                    }

                    
                    product.StockAvailable -= delta;

                   
                    product.PartitionKey = ProductPartition;

                    
                    await _storageService.UpdateEntityAsync(product);

                   
                    var stockMessage = JsonSerializer.Serialize(new
                    {
                        ProductId = product.RowKey,
                        product.ProductName,
                        product.StockAvailable,
                        Action = "StockUpdated",
                        Timestamp = DateTime.UtcNow
                    });
                    await _storageService.SendMessageAsync("stock-updates", stockMessage);
                }

               
                existingOrder.ProductId = model.ProductId;
                existingOrder.ProductName = product.ProductName;
                existingOrder.Quantity = model.Quantity;
                existingOrder.UnitPrice = product.Price;
                existingOrder.TotalPrice = product.Price * model.Quantity;

                
                if (model.OrderDate != default)
                    existingOrder.OrderDate = DateTime.SpecifyKind(model.OrderDate, DateTimeKind.Utc);

               
                if (!string.IsNullOrWhiteSpace(model.Status))
                    existingOrder.Status = model.Status;

               
                await _storageService.UpdateEntityAsync(existingOrder);

                
                var orderMessage = JsonSerializer.Serialize(new
                {
                    OrderId = existingOrder.RowKey,
                    existingOrder.CustomerId,
                    existingOrder.Username,
                    existingOrder.ProductId,
                    existingOrder.ProductName,
                    existingOrder.Quantity,
                    existingOrder.TotalPrice,
                    existingOrder.Status,
                    Action = "Updated",
                    Timestamp = DateTime.UtcNow
                });
                await _storageService.SendMessageAsync("order-notifications", orderMessage);

                TempData["Success"] = "Order updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order: {Message}", ex.Message);
                ModelState.AddModelError(string.Empty, $"Error updating order: {ex.Message}");
                await PopulateDropdowns(model);
                return View(model);
            }
        }


        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest();

            try
            {
                var order = await _storageService.GetEntityAsync<Order>(OrderPartition, id);
                if (order == null)
                    return NotFound();

                return View(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading order for delete: {Message}", ex.Message);
                TempData["Error"] = $"Error loading order: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest();

            try
            {
                var order = await _storageService.GetEntityAsync<Order>(OrderPartition, id);
                if (order != null)
                {
                    var product = await _storageService.GetEntityAsync<Product>(ProductPartition, order.ProductId);
                    if (product != null)
                    {
                      
                        product.StockAvailable += order.Quantity;
                        await _storageService.UpdateEntityAsync(product);

                      
                        var stockMessage = JsonSerializer.Serialize(new
                        {
                            ProductId = product.RowKey,
                            product.ProductName,
                            product.StockAvailable,
                            Action = "StockRestored",
                            Timestamp = DateTime.UtcNow
                        });
                        await _storageService.SendMessageAsync("stock-updates", stockMessage);
                    }

                    
                    await _storageService.DeleteEntityAsync<Order>(OrderPartition, id);

                   
                    var orderMessage = JsonSerializer.Serialize(new
                    {
                        OrderId = order.RowKey,
                        order.CustomerId,
                        order.Username,
                        order.ProductId,
                        order.ProductName,
                        order.Quantity,
                        order.TotalPrice,
                        order.Status,
                        Action = "Deleted",
                        Timestamp = DateTime.UtcNow
                    });
                    await _storageService.SendMessageAsync("order-notifications", orderMessage);

                    TempData["Success"] = "Order deleted and stock restored.";
                }
                else
                {
                    TempData["Error"] = "Order not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting order: {Message}", ex.Message);
                TempData["Error"] = $"Error deleting order: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }


        #region Helpers


        private async Task PopulateDropdowns(OrderCreateViewModel model)
        {
            try
            {
                var customers = (await _storageService.GetAllEntitiesAsync<Customer>()).ToList();
                var products = (await _storageService.GetAllEntitiesAsync<Product>()).ToList();

               
                if (model != null)
                {
                    model.Customers = customers;
                    model.Products = products;
                }

                
                ViewBag.Customers = customers;
                ViewBag.Products = products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating dropdowns: {Message}", ex.Message);
               
                ViewBag.Customers = new List<Customer>();
                ViewBag.Products = new List<Product>();
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetProductPrice(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
                return BadRequest(new { error = "productId required" });

            try
            {
                var product = await _storageService.GetEntityAsync<Product>(ProductPartition, productId);
                if (product == null)
                    return NotFound(new { error = "Product not found" });

                return Json(new
                {
                    price = product.Price,
                    stock = product.StockAvailable,
                    productName = product.ProductName,
                    imageUrl = product.ImageUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching product price for {ProductId}: {Message}", productId, ex.Message);
                return StatusCode(500, new { error = "Error fetching product info" });
            }
        }
        #endregion
    }
}

