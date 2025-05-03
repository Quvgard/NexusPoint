using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusPoint.Models
{
    public class Check
    {
        public int CheckId { get; set; }
        public int ShiftId { get; set; } // <<--- ДОБАВЛЕНО
        public int CheckNumber { get; set; }
        public DateTime Timestamp { get; set; }
        public int UserId { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentType { get; set; } // "Cash", "Card", "Mixed"
        public decimal CashPaid { get; set; }
        public decimal CardPaid { get; set; }
        public decimal DiscountAmount { get; set; }
        public bool IsReturn { get; set; }
        public int? OriginalCheckId { get; set; }

        // Навигационное свойство
        public List<CheckItem> Items { get; set; } = new List<CheckItem>();
        // Опционально
        // public User Cashier { get; set; }
        // public Shift Shift { get; set; }
    }
}
