using System;
using System.Threading.Tasks;
using Contracts;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using OrderService.Models;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IPublishEndpoint _publishEndpoint;

        public OrderController(IPublishEndpoint publishEndpoint)
        {
            _publishEndpoint = publishEndpoint;
        }

        [HttpPost]
        public async Task<IActionResult> SubmitOrder([FromBody] OrderRequest request)
        {
            var orderId = Guid.NewGuid();

            await _publishEndpoint.Publish<OrderSubmitted>(new
            {
                OrderId = orderId,
                Timestamp = DateTime.UtcNow,
                CustomerNumber = request.CustomerNumber,
                TotalAmount = request.TotalAmount
            });

            return Accepted(new { OrderId = orderId });
        }
    }
}
