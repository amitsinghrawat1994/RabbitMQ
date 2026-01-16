using System;
using MassTransit;

namespace OrderService.Sagas
{
    public class OrderState : SagaStateMachineInstance
    {
        public Guid CorrelationId { get; set; }
        public string CurrentState { get; set; } = null!;

        public string OrderId { get; set; } = null!;

        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }

        public string CustomerNumber { get; set; } = null!;
        public decimal TotalAmount { get; set; }
        public Guid? PaymentId { get; set; } // Just an example of extra data


    }
}
