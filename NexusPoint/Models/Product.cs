﻿namespace NexusPoint.Models
{
    public class Product
    {
        public int ProductId { get; set; }
        public string Barcode { get; set; }
        public string ProductCode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
    }
}
