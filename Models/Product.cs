﻿using System;
using System.ComponentModel.DataAnnotations;
using Azure;
using Azure.Data.Tables;

namespace ABCRetailers.Models
{
    public class Product : ITableEntity
    {
        public string PartitionKey { get; set; } = "Product";
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        [Display(Name = "Product ID")]
        public string Id => RowKey;

        [Required(ErrorMessage = "Product name is required")]
        [Display(Name = "Product Name")]
        public string ProductName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required")]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be a value greater than 0.01")]
        [Display(Name = "Price")]
        public decimal Price { get; set; }

        public string PriceString
        {
            get => Price.ToString("F2");
            set
            {
                if (decimal.TryParse(value, out decimal result))
                    Price = result;
                else
                    Price = 0m;
            }
        }

        [Required(ErrorMessage = "Stock available is required")]
        [Display(Name = "Stock Available")]
        public int StockAvailable { get; set; }

        [Display(Name = "Image Url")]
        public string ImageUrl { get; set; } = string.Empty;
    }
}