using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusPoint.Models
{
    public class CashDrawerOperation
    {
        public int OperationId { get; set; }
        public int ShiftId { get; set; }
        public int UserId { get; set; }
        public DateTime Timestamp { get; set; }
        public string OperationType { get; set; } // "CashIn", "CashOut"
        public decimal Amount { get; set; }
        public string Reason { get; set; }

        // Навигационные свойства (опционально)
        // public Shift Shift { get; set; }
        // public User User { get; set; }
    }
}
