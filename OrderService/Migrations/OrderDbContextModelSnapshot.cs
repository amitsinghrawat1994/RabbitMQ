using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OrderService.Sagas;

#nullable disable

namespace OrderService.Migrations
{
    [DbContext(typeof(OrderDbContext))]
    partial class OrderDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "EFCore")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("OrderService.Sagas.OrderState", b =>
            {
                b.Property<Guid>("CorrelationId");

                b.Property<string>("CurrentState")
                    .IsRequired()
                    .HasMaxLength(64);

                b.Property<string>("OrderId")
                    .IsRequired()
                    .HasMaxLength(64);

                b.Property<DateTime>("Created").IsRequired();

                b.Property<DateTime>("Updated").IsRequired();

                b.Property<string>("CustomerNumber")
                    .IsRequired()
                    .HasMaxLength(256);

                b.Property<decimal>("TotalAmount").IsRequired();

                b.Property<Guid?>("PaymentId");

                b.HasKey("CorrelationId");

                b.ToTable("OrderState");
            });
        }
    }
}
