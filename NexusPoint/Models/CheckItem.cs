using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusPoint.Models
{
    public class CheckItem
    {
        public int CheckItemId { get; set; }
        public int CheckId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal PriceAtSale { get; set; }
        public decimal ItemTotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }

        // Навигационное свойство (опционально)
        // public Product Product { get; set; }
        // public Check Check { get; set; }
    }
}
