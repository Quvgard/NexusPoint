using System;

namespace NexusPoint.Models
{
    public class Shift
    {
        public int ShiftId { get; set; }
        public int ShiftNumber { get; set; }
        public DateTime OpenTimestamp { get; set; }
        public DateTime? CloseTimestamp { get; set; }
        public int OpeningUserId { get; set; }
        public int? ClosingUserId { get; set; }
        public decimal StartCash { get; set; }
        public decimal? TotalSales { get; set; }
        public decimal? TotalReturns { get; set; }
        public decimal? CashSales { get; set; }
        public decimal? CardSales { get; set; }
        public decimal? CashAdded { get; set; }
        public decimal? CashRemoved { get; set; }
        public decimal? EndCashTheoretic { get; set; }
        public decimal? EndCashActual { get; set; }
        public decimal? Difference { get; set; }
        public bool IsClosed { get; set; }
    }
}
