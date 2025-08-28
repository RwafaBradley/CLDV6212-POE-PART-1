using System.ComponentModel.DataAnnotations;
using ABCRetailers.Services;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ABCRetailers.Models;

namespace ABCRetailers.Controllers
{
    public class CustomerController : Controller
    {
        private readonly IAzureStorageService _storageService;
        private const string CustomerPartition = "Customer";

        public CustomerController(IAzureStorageService storageService)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        }

        
        public async Task<IActionResult> Index()
        {
            var customers = await _storageService.GetAllEntitiesAsync<Customer>();
            return View(customers);
        }

        
        public IActionResult Create()
        {
            return View();
        }

       
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            if (!ModelState.IsValid)
                return View(customer);

            try
            {

                customer.PartitionKey = CustomerPartition;               
                if (string.IsNullOrWhiteSpace(customer.RowKey))
                    customer.RowKey = Guid.NewGuid().ToString();

               
                await _storageService.AddEntityAsync(customer);
                TempData["Success"] = "Customer created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Error creating customer: {ex.Message}");
                return View(customer);
            }
        }
        
       
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest();

            try
            {

                var customer = await _storageService.GetEntityAsync<Customer>(CustomerPartition, id);
                if (customer == null)
                    return NotFound();

                return View(customer);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading customer: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Customer customer)
        {
            if (!ModelState.IsValid)
                return View(customer);

            try
            {
               
                var existing = await _storageService.GetEntityAsync<Customer>(CustomerPartition, customer.RowKey);
                if (existing == null)
                {
                    ModelState.AddModelError(string.Empty, "Customer not found.");
                    return View(customer);
                }

                existing.Name = customer.Name;
                existing.Surname = customer.Surname;
                existing.Username = customer.Username;
                existing.ShippingAddress = customer.ShippingAddress;
                existing.EmailAddress = customer.EmailAddress;

                await _storageService.UpdateEntityAsync(existing);
                TempData["Success"] = "Customer updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Error updating customer: {ex.Message}");
                return View(customer);
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
                await _storageService.DeleteEntityAsync<Customer>("Customer", id);
                TempData["Success"] = "Customer deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting customer: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
