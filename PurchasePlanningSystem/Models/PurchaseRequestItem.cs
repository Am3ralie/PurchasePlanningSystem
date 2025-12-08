namespace PurchasePlanningSystem.Models
{
    public class PurchaseRequestItem
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public DateTime RequiredDate { get; set; }
        public string? Notes { get; set; }
    }
}