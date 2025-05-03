using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusPoint.Models
{
    public class StockItem
    {
        public int StockItemId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; } // decimal для точности
        public DateTime LastUpdated { get; set; }

        // Навигационное свойство (опционально, для удобства)
        // public Product Product { get; set; }
    }
}
