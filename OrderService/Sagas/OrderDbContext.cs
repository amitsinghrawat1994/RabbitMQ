using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MassTransit.EntityFrameworkCoreIntegration;

namespace OrderService.Sagas
{
    public class OrderDbContext : SagaDbContext
    {
        public OrderDbContext(DbContextOptions options)
            : base(options)
        {
        }

        protected override IEnumerable<ISagaClassMap> Configurations
        {
            get { yield return new OrderStateMap(); }
        }
    }
}