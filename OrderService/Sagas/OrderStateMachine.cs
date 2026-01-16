using System;
using Contracts;
using MassTransit;

namespace OrderService.Sagas
{
    public class OrderStateMachine : MassTransitStateMachine<OrderState>
    {
        public OrderStateMachine()
        {
            // correlate events to the saga instance (messages carry string OrderId — correlate by saga.OrderId)
            Event(() => OrderSubmitted, x => x.CorrelateBy((instance, context) => instance.OrderId == context.Message.OrderId)
                .SelectId(context => Guid.NewGuid()));
            Event(() => StockReserved, x => x.CorrelateBy((instance, context) => instance.OrderId == context.Message.OrderId));
            Event(() => StockShortage, x => x.CorrelateBy((instance, context) => instance.OrderId == context.Message.OrderId));
            Event(() => PaymentAccepted, x => x.CorrelateBy((instance, context) => instance.OrderId == context.Message.OrderId));
            Event(() => PaymentFailed, x => x.CorrelateBy((instance, context) => instance.OrderId == context.Message.OrderId));


            // Define states
            InstanceState(x => x.CurrentState);

            Initially(
                When(OrderSubmitted)
                    .Then(context =>
                    {
                        context.Saga.OrderId = context.Message.OrderId;
                        context.Saga.Created = DateTime.UtcNow;
                        context.Saga.Updated = DateTime.UtcNow;
                        context.Saga.CustomerNumber = context.Message.CustomerNumber;
                        context.Saga.TotalAmount = context.Message.TotalAmount;
                    })
                    .TransitionTo(Submitted)
                    // Use PublishAsync with Init for clean interface publishing
                    .PublishAsync(context => context.Init<CheckInventory>(new
                    {
                        OrderId = context.Saga.OrderId
                    }))
            );

            During(Submitted,
                When(StockReserved)
                    .Then(context => context.Saga.Updated = DateTime.UtcNow)
                    .TransitionTo(InventoryReserved)
                    .PublishAsync(context => context.Init<ProcessPayment>(new
                    {
                        OrderId = context.Saga.OrderId,
                        Amount = context.Saga.TotalAmount,
                        CardNumber = "1234-5678-9012-3456"
                    })),

                When(StockShortage)
                    .Then(context => context.Saga.Updated = DateTime.UtcNow)
                    .TransitionTo(Failed)
                    .PublishAsync(context => context.Init<OrderFailed>(new
                    {
                        OrderId = context.Saga.OrderId,
                        Reason = context.Message.Reason
                    }))
            );

            During(InventoryReserved,
                When(PaymentAccepted)
                    .Then(context => context.Saga.Updated = DateTime.UtcNow)
                    .TransitionTo(Completed)
                    .PublishAsync(context => context.Init<OrderCompleted>(new
                    {
                        OrderId = context.Saga.CorrelationId.ToString()
                    }))
                    .Finalize(),

                When(PaymentFailed)
                    .Then(context => context.Saga.Updated = DateTime.UtcNow)
                    .TransitionTo(Failed)
                    .PublishAsync(context => context.Init<OrderFailed>(new
                    {
                        OrderId = context.Saga.OrderId,
                        Reason = context.Message.Reason
                    }))
            );

            SetCompletedWhenFinalized();
        }

        public State Submitted { get; private set; } = null!;
        public State InventoryReserved { get; private set; } = null!;
        public State Completed { get; private set; } = null!;
        public State Failed { get; private set; } = null!;

        public Event<OrderSubmitted> OrderSubmitted { get; private set; } = null!;
        public Event<StockReserved> StockReserved { get; private set; } = null!;
        public Event<StockShortage> StockShortage { get; private set; } = null!;
        public Event<PaymentAccepted> PaymentAccepted { get; private set; } = null!;
        public Event<PaymentFailed> PaymentFailed { get; private set; } = null!;
    }
}
