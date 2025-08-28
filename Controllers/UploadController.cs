using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ABCRetailers.Models;
using ABCRetailers.Services;

namespace ABCRetailers.Controllers
{
    public class UploadController : Controller
    {
        private readonly IAzureStorageService _storageService;
        private readonly ILogger<UploadController> _logger;

        private const string OrderPartition = "Order";

        public UploadController(IAzureStorageService storageService, ILogger<UploadController> logger)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = new FileUploadModel();

            // Populate orders and customers dropdowns
            await PopulateDropdowns(model);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(FileUploadModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(model);
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(model.OrderId))
            {
                ModelState.AddModelError("OrderId", "Please select an order.");
                await PopulateDropdowns(model);
                return View(model);
            }

            try
            {
                if (model?.ProofOfPayment == null || model.ProofOfPayment.Length == 0)
                {
                    ModelState.AddModelError("ProofOfPayment", "Please select a file to upload.");
                    await PopulateDropdowns(model);
                    return View(model);
                }

                // Upload to blob storage
                var fileUrl = await _storageService.UploadFileAsync(model.ProofOfPayment, "proof-of-payments");

                // Optional: Upload to file share
                try
                {
                    await _storageService.UploadToFileShareAsync(model.ProofOfPayment, "contracts");
                }
                catch (NotImplementedException)
                {
                    _logger.LogDebug("UploadToFileShareAsync not implemented; skipping file-share upload.");
                }
                catch (Exception fsEx)
                {
                    _logger.LogWarning(fsEx, "File-share upload failed (non-fatal): {Message}", fsEx.Message);
                }

                // ✅ Update order status to Completed
                var order = await _storageService.GetEntityAsync<Order>(OrderPartition, model.OrderId);
                if (order != null)
                {
                    order.Status = "Completed";
                    await _storageService.UpdateEntityAsync(order);
                }

                TempData["Success"] = $"File uploaded successfully! File: {fileUrl}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file: {Message}", ex.Message);
                ModelState.AddModelError(string.Empty, $"Error uploading file: {ex.Message}");
                await PopulateDropdowns(model);
                return View(model);
            }
        }

        #region Helpers

        private async Task PopulateDropdowns(FileUploadModel model)
        {
            try
            {
                var orders = (await _storageService.GetAllEntitiesAsync<Order>())
                                .OrderByDescending(o => o.OrderDate)
                                .ToList();

                var customers = orders
                                .Select(o => o.Username)
                                .Distinct()
                                .ToList();

                model.Orders = orders;
                model.Customers = customers;

                ViewBag.Orders = orders;
                ViewBag.Customers = customers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating dropdowns: {Message}", ex.Message);
                ViewBag.Orders = Enumerable.Empty<Order>();
                ViewBag.Customers = Enumerable.Empty<string>();
            }
        }

        #endregion
    }
}
