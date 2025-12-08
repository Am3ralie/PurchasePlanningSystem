namespace PurchasePlanningSystem.Models
{
    public class PurchaseRequestViewModel
    {
        public int Id { get; set; }
        public string? Number { get; set; } // Добавляем "?" для nullable
        public DateTime Date { get; set; }
        public string? Status { get; set; } // Добавляем "?" для nullable
        public string? CreatedBy { get; set; } // Добавляем "?" для nullable
    }
}