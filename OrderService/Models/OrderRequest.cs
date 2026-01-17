using System.ComponentModel.DataAnnotations;

namespace OrderService.Models
{
    public class OrderRequest
    {
        public string? OrderId { get; set; }

        [Required]
        [StringLength(256, MinimumLength = 1)]
        public string? CustomerNumber { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "TotalAmount must be greater than 0")]
        public decimal TotalAmount { get; set; }
    }
}
