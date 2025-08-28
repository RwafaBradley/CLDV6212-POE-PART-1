using System;
using System.Text.Json;
using System.Threading.Tasks;
using ABCRetailers.Models;     
using ABCRetailers.Services;  
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ABCRetailers.Controllers
{
    
    public class ProductController : Controller
    {
        private readonly IAzureStorageService _storageService;
        private readonly ILogger<ProductController> _logger;
        private const string ProductPartition = "Product";

        public ProductController(IAzureStorageService storageService,
                                 ILogger<ProductController> logger)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        
        public async Task<IActionResult> Index()
        {
            try
            {
                var products = await _storageService.GetAllEntitiesAsync<Product>();
                return View(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products: {Message}", ex.Message);
                TempData["Error"] = $"Error loading products: {ex.Message}";
                return View(Array.Empty<Product>());
            }
        }

        
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
        {
            if (Request.Form.TryGetValue("Price", out var priceFormValue) && !string.IsNullOrWhiteSpace(priceFormValue))
            {
                if (decimal.TryParse(priceFormValue, out var parsedPrice))
                    product.Price = parsedPrice;
                else
                    _logger.LogWarning("Failed to parse price form value: {RawPrice}", priceFormValue.ToString());
            }

            _logger.LogInformation("Final product price: {Price}", product?.Price);

            if (!ModelState.IsValid)
                return View(product);

            if (product.Price <= 0)
            {
                ModelState.AddModelError("Price", "Price must be greater than 0.00");
                return View(product);
            }

            try
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    var imageUrl = await _storageService.UploadImageAsync(imageFile, "product-images");
                    product.ImageUrl = imageUrl;
                }

                
                product.PartitionKey = ProductPartition;
                if (string.IsNullOrWhiteSpace(product.RowKey))
                    product.RowKey = Guid.NewGuid().ToString();

                await _storageService.AddEntityAsync(product);

                TempData["Success"] = $"Product '{product.ProductName}' created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product: {Message}", ex.Message);
                ModelState.AddModelError(string.Empty, $"Error creating product: {ex.Message}");
                return View(product);
            }
        }


        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest();

            try
            {
                
                var product = await _storageService.GetEntityAsync<Product>(ProductPartition, id);
                if (product == null)
                    return NotFound();

                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading product for edit: {Message}", ex.Message);
                TempData["Error"] = $"Error loading product: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Product postedProduct, IFormFile? imageFile)
        {
            if (!ModelState.IsValid)
                return View(postedProduct);

            if (postedProduct.Price <= 0)
            {
                ModelState.AddModelError("Price", "Price must be greater than 0.00");
                return View(postedProduct);
            }

            try
            {
                var existing = await _storageService.GetEntityAsync<Product>(ProductPartition, postedProduct.RowKey);
                if (existing == null)
                    return NotFound();

               
                if (imageFile != null && imageFile.Length > 0)
                {
                    existing.ImageUrl = await _storageService.UploadImageAsync(imageFile, "product-images");
                }

                
                existing.ProductName = postedProduct.ProductName;
                existing.Description = postedProduct.Description;
                existing.Price = postedProduct.Price;
                existing.StockAvailable = postedProduct.StockAvailable;

               
                await _storageService.UpdateEntityAsync(existing);

               
                var productMessage = JsonSerializer.Serialize(new
                {
                    ProductId = existing.RowKey,
                    existing.ProductName,
                    existing.Price,
                    existing.StockAvailable,
                    Action = "Updated",
                    Timestamp = DateTime.UtcNow
                });
                await _storageService.SendMessageAsync("stock-updates", productMessage);

                TempData["Success"] = "Product updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product: {Message}", ex.Message);
                ModelState.AddModelError(string.Empty, $"Error updating product: {ex.Message}");
                return View(postedProduct);
            }
        }

        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest();

            try
            {
                var product = await _storageService.GetEntityAsync<Product>("Product", id);
                if (product == null)
                    return NotFound();

                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading product for delete: {Message}", ex.Message);
                TempData["Error"] = $"Error loading product: {ex.Message}";
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
                await _storageService.DeleteEntityAsync<Product>("Product", id);
                TempData["Success"] = "Product deleted successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product: {Message}", ex.Message);
                TempData["Error"] = $"Error deleting product: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
