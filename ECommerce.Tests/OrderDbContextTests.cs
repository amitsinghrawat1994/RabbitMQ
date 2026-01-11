using System.Linq;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using OrderService.Sagas;
using Xunit;

namespace ECommerce.Tests
{
    public class OrderDbContextTests
    {
        [Fact]
        public void Should_Have_OrderStateMap_Configuration()
        {
            var options = new DbContextOptionsBuilder<OrderDbContext>()
                .UseInMemoryDatabase(databaseName: "OrderDb_Test")
                .Options;

            using var context = new OrderDbContext(options);
            
            // Access the protected property via a subclass or just verifying the model creation works.
            // Since Configurations is protected, we can't assert it directly easily without reflection or subclass.
            // But we can verify the Model has the entity.
            
            // Force model creation
            var model = context.Model;
            var entityType = model.FindEntityType(typeof(OrderState));
            
            Assert.NotNull(entityType);
            
            // Check specific mappings from OrderStateMap
            var currentStateProp = entityType.FindProperty(nameof(OrderState.CurrentState));
            Assert.NotNull(currentStateProp);
            Assert.Equal(64, currentStateProp.GetMaxLength());

            var customerNumberProp = entityType.FindProperty(nameof(OrderState.CustomerNumber));
            Assert.NotNull(customerNumberProp);
            Assert.Equal(256, customerNumberProp.GetMaxLength());
        }
    }
}
