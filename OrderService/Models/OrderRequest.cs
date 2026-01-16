namespace OrderService.Models
{
    public class OrderRequest
    {
        public string? OrderId { get; set; }
        public string CustomerNumber { get; set; } = null!;
        public decimal TotalAmount { get; set; }
    }
}
