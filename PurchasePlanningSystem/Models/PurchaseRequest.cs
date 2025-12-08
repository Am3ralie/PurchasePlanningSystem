namespace PurchasePlanningSystem.Models
{
    public class PurchaseRequest
    {
        public int Id { get; set; }
        public string? Number { get; set; }
        public DateTime Date { get; set; }
        public string? Status { get; set; }
        public int CreatedByUserId { get; set; }
        public int? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? Comments { get; set; }
    }
}