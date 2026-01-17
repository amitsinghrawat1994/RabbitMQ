using System;
using System.Threading.Tasks;
using Contracts;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using OrderService.Models;
using Microsoft.EntityFrameworkCore;
using OrderService.Sagas;
using System.ComponentModel.DataAnnotations;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly OrderDbContext _db;

        public OrderController(IPublishEndpoint publishEndpoint, OrderDbContext db)
        {
            _publishEndpoint = publishEndpoint;
            _db = db;
        }

        [HttpPost]
        public async Task<IActionResult> SubmitOrder([FromBody] OrderRequest request)
        {
            // Server-side validation using DataAnnotations (works in unit tests)
            var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(request);
            var validationResults = new System.Collections.Generic.List<System.ComponentModel.DataAnnotations.ValidationResult>();
            if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request, validationContext, validationResults, true))
            {
                foreach (var vr in validationResults)
                {
                    var member = vr.MemberNames != null ? string.Join(",", vr.MemberNames) : string.Empty;
                    ModelState.AddModelError(member, vr.ErrorMessage ?? "Invalid");
                }
                return BadRequest(ModelState);
            }

            var orderId = request.OrderId ?? Guid.NewGuid().ToString();

            await _publishEndpoint.Publish<OrderSubmitted>(new
            {
                OrderId = orderId,
                Timestamp = DateTime.UtcNow,
                CustomerNumber = request.CustomerNumber,
                TotalAmount = request.TotalAmount
            });

            return Accepted(new { OrderId = orderId });
        }

        [HttpGet("{orderId}")]
        public async Task<IActionResult> GetStatus(string orderId)
        {
            // First, check the persisted orders table for completed/failed states
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order != null)
                return Ok(new { order.OrderId, order.Status, order.CompletedAt, order.Reason, order.TotalAmount, order.CustomerNumber });

            // Fall back to saga state for in-progress orders
            var saga = await _db.Set<OrderState>().FirstOrDefaultAsync(s => s.OrderId == orderId);
            if (saga != null)
                return Ok(new { saga.OrderId, Status = saga.CurrentState, saga.Updated });

            return NotFound();
        }
    }
}
