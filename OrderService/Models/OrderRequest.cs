namespace OrderService.Models
{
    public class OrderRequest
    {
        public string CustomerNumber { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
