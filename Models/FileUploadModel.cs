using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ABCRetailers.Models
{
    public class FileUploadModel
    {
        [Required]
        [Display(Name = "Proof of payment")]
        public IFormFile? ProofOfPayment { get; set; }

        [Display(Name = "Order Id")]
        public string? OrderId { get; set; }

        [Display(Name = "Customer Name")]
        public string? CustomerName { get; set; }

        public List<Order> Orders { get; set; } = new();
        public List<string> Customers { get; set; } = new();
    }
}

