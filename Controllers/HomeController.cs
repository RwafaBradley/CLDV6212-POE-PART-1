using System.Diagnostics;
using ABCRetailers.Models;
using ABCRetailers.Models.ViewModels;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetailers.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

       

        private readonly IAzureStorageService _storageService;

        public HomeController(IAzureStorageService storageService)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        }

        public async Task<IActionResult> Index()
        {
            
            var vm = new HomeViewModel();

            
            var products = (await _storageService.GetAllEntitiesAsync<Product>()).ToList();
            var customers = (await _storageService.GetAllEntitiesAsync<Customer>()).ToList();
            var orders = (await _storageService.GetAllEntitiesAsync<Order>()).ToList();

            vm.FeaturedProducts = products.Take(5).ToList();
            vm.ProductCount = products.Count;
            vm.CustomerCount = customers.Count;
            vm.OrderCount = orders.Count;

            return View(vm);
        }

        public IActionResult Privacy() => View();


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
