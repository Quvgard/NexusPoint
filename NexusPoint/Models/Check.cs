using System;
using System.Collections.Generic;

namespace NexusPoint.Models
{
    public class Check
    {
        public int CheckId { get; set; }
        public int ShiftId { get; set; }
        public int CheckNumber { get; set; }
        public DateTime Timestamp { get; set; }
        public int UserId { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentType { get; set; }
        public decimal CashPaid { get; set; }
        public decimal CardPaid { get; set; }
        public decimal DiscountAmount { get; set; }
        public bool IsReturn { get; set; }
        public int? OriginalCheckId { get; set; }
        public List<CheckItem> Items { get; set; } = new List<CheckItem>();
    }
}
