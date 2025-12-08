namespace PurchasePlanningSystem.Models
{
    public class PurchaseOrder
    {
        public int Id { get; set; }
        public string? Number { get; set; }
        public DateTime Date { get; set; }
        public string? Status { get; set; }
        public int SupplierId { get; set; }
        public int RequestId { get; set; }
        public int CreatedByUserId { get; set; }
        public decimal TotalAmount { get; set; }
    }
}