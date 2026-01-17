using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MassTransit.EntityFrameworkCoreIntegration;

namespace OrderService.Sagas
{
    using OrderService.Models;

    public class OrderDbContext : SagaDbContext
    {
        public OrderDbContext(DbContextOptions options)
            : base(options)
        {
        }

        // Persist completed orders for audit and status lookup
        public DbSet<Order> Orders { get; set; } = null!;

        protected override IEnumerable<ISagaClassMap> Configurations
        {
            get { yield return new OrderStateMap(); }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Unique index to ensure OrderId is unique for quick lookups and integrity
            modelBuilder.Entity<Order>().HasIndex(o => o.OrderId).IsUnique();
        }
    }
}