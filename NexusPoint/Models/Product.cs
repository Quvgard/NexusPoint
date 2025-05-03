using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusPoint.Models
{
    public class Product // Это наш "каталог"
    {
        public int ProductId { get; set; }
        public string Barcode { get; set; }
        public string ProductCode { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; } // decimal для денег
        public bool IsMarked { get; set; }
    }
}
