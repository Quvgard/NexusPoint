using System;

namespace NexusPoint.Models
{
    public class StockItem
    {
        public int StockItemId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
