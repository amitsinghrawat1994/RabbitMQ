using System;
using MassTransit;

namespace OrderService.Sagas
{
    public class OrderState : SagaStateMachineInstance
    {
        public Guid CorrelationId { get; set; }
        public string CurrentState { get; set; }

        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }

        public string CustomerNumber { get; set; }
        public decimal TotalAmount { get; set; }
        public Guid? PaymentId { get; set; } // Just an example of extra data
        
        public byte[] RowVersion { get; set; }
    }
}
