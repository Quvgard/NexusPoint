using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusPoint.Models
{
    public class Shift
    {
        public int ShiftId { get; set; }
        public int ShiftNumber { get; set; }
        public DateTime OpenTimestamp { get; set; }
        public DateTime? CloseTimestamp { get; set; } // Nullable, если смена не закрыта
        public int OpeningUserId { get; set; }
        public int? ClosingUserId { get; set; } // Nullable
        public decimal StartCash { get; set; }
        public decimal? TotalSales { get; set; } // Nullable, рассчитывается при закрытии
        public decimal? TotalReturns { get; set; } // Nullable
        public decimal? CashSales { get; set; } // Nullable
        public decimal? CardSales { get; set; } // Nullable
        public decimal? CashAdded { get; set; } // Nullable
        public decimal? CashRemoved { get; set; } // Nullable
        public decimal? EndCashTheoretic { get; set; } // Nullable
        public decimal? EndCashActual { get; set; } // Nullable
        public decimal? Difference { get; set; } // Nullable
        public bool IsClosed { get; set; }

        // Навигационные свойства (опционально)
        // public User OpeningUser { get; set; }
        // public User ClosingUser { get; set; }
    }
}
