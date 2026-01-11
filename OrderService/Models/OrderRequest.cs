namespace OrderService.Models
{
    public class OrderRequest
    {
        public string CustomerNumber { get; set; } = null!;
        public decimal TotalAmount { get; set; }
    }
}
