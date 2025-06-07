using System;

namespace NexusPoint.Models
{
    public class Discount
    {
        public int DiscountId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? Value { get; set; }
        public int? RequiredProductId { get; set; }
        public int? GiftProductId { get; set; }
        public int? RequiredQuantityN { get; set; }
        public int? GiftQuantityM { get; set; }
        public int? NthItemNumber { get; set; }
        public bool IsNthDiscountPercentage { get; set; }
        public decimal? CheckAmountThreshold { get; set; }
        public bool IsCheckDiscountPercentage { get; set; }
    }
}