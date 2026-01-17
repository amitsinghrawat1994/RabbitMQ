using System;

namespace OrderService.Models
{
    public class Order
    {
        public int Id { get; set; }
        public string OrderId { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string? CustomerNumber { get; set; }
        public decimal? TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Reason { get; set; }
    }
}
