using System;
using Contracts;
using MassTransit;

namespace OrderService.Sagas
{
    public class OrderStateMachine : MassTransitStateMachine<OrderState>
    {
        public OrderStateMachine()
        {
            // correlate events to the saga instance
            Event(() => OrderSubmitted, x => x.CorrelateById(m => m.Message.OrderId));
            Event(() => StockReserved, x => x.CorrelateById(m => m.Message.OrderId));
            Event(() => StockShortage, x => x.CorrelateById(m => m.Message.OrderId));
            Event(() => PaymentAccepted, x => x.CorrelateById(m => m.Message.OrderId));
            Event(() => PaymentFailed, x => x.CorrelateById(m => m.Message.OrderId));

            // Define states
            InstanceState(x => x.CurrentState);

            Initially(
                When(OrderSubmitted)
                    .Then(context =>
                    {
                        context.Saga.Created = DateTime.UtcNow;
                        context.Saga.Updated = DateTime.UtcNow;
                        context.Saga.CustomerNumber = context.Message.CustomerNumber;
                        context.Saga.TotalAmount = context.Message.TotalAmount;
                    })
                    .TransitionTo(Submitted)
                    // Use PublishAsync with Init for clean interface publishing
                    .PublishAsync(context => context.Init<CheckInventory>(new 
                    { 
                        OrderId = context.Saga.CorrelationId 
                    }))
            );

            During(Submitted,
                When(StockReserved)
                    .Then(context => context.Saga.Updated = DateTime.UtcNow)
                    .TransitionTo(InventoryReserved)
                    .PublishAsync(context => context.Init<ProcessPayment>(new 
                    { 
                        OrderId = context.Saga.CorrelationId,
                        Amount = context.Saga.TotalAmount,
                        CardNumber = "1234-5678-9012-3456" 
                    })),
                
                When(StockShortage)
                    .Then(context => context.Saga.Updated = DateTime.UtcNow)
                    .TransitionTo(Failed)
                    .PublishAsync(context => context.Init<OrderFailed>(new 
                    { 
                        OrderId = context.Saga.CorrelationId,
                        Reason = context.Message.Reason
                    }))
            );

            During(InventoryReserved,
                When(PaymentAccepted)
                    .Then(context => context.Saga.Updated = DateTime.UtcNow)
                    .TransitionTo(Completed)
                    .PublishAsync(context => context.Init<OrderCompleted>(new 
                    { 
                        OrderId = context.Saga.CorrelationId 
                    }))
                    .Finalize(), 

                When(PaymentFailed)
                    .Then(context => context.Saga.Updated = DateTime.UtcNow)
                    .TransitionTo(Failed)
                    .PublishAsync(context => context.Init<OrderFailed>(new 
                    { 
                        OrderId = context.Saga.CorrelationId,
                        Reason = context.Message.Reason
                    }))
            );

            SetCompletedWhenFinalized();
        }

        public State Submitted { get; private set; }
        public State InventoryReserved { get; private set; }
        public State Completed { get; private set; }
        public State Failed { get; private set; }

        public Event<OrderSubmitted> OrderSubmitted { get; private set; }
        public Event<StockReserved> StockReserved { get; private set; }
        public Event<StockShortage> StockShortage { get; private set; }
        public Event<PaymentAccepted> PaymentAccepted { get; private set; }
        public Event<PaymentFailed> PaymentFailed { get; private set; }
    }
}
