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
        public int? AppliedDiscountId { get; set; }
    }
}
