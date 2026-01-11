using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ECommerce.Tests
{
    public class WorkerTests
    {
        [Fact]
        public async Task Inventory_Worker_Should_Execute()
        {
            var mockLogger = new Mock<ILogger<InventoryService.Worker>>();
            mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
            var worker = new InventoryService.Worker(mockLogger.Object);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(100); // Run for a short time

            // ExecuteAsync is protected. 
            // Standard way to test Worker is calling StartAsync / StopAsync.
            
            await worker.StartAsync(cts.Token);
            await Task.Delay(150); // Let it run a loop
            await worker.StopAsync(CancellationToken.None);

            // It should have logged "Worker running at"
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Worker running at")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task Payment_Worker_Should_Execute()
        {
            var mockLogger = new Mock<ILogger<PaymentService.Worker>>();
            mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
            var worker = new PaymentService.Worker(mockLogger.Object);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(100);

            await worker.StartAsync(cts.Token);
            await Task.Delay(150);
            await worker.StopAsync(CancellationToken.None);

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Worker running at")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
    }
}
