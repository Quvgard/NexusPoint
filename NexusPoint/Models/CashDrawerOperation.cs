using System;

namespace NexusPoint.Models
{
    public class CashDrawerOperation
    {
        public int OperationId { get; set; }
        public int ShiftId { get; set; }
        public int UserId { get; set; }
        public DateTime Timestamp { get; set; }
        public string OperationType { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; }
    }
}
