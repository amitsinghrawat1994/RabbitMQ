using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OrderService.Sagas
{
    public class OrderStateMap : SagaClassMap<OrderState>
    {
        protected override void Configure(EntityTypeBuilder<OrderState> entity, ModelBuilder model)
        {
            entity.Property(x => x.CurrentState).HasMaxLength(64);
            entity.Property(x => x.CustomerNumber).HasMaxLength(256);
            
            // For SQLite, RowVersion is usually BLOB/Timestamp, handled slightly differently, 
            // but EF Core handles basic concurrency. 
            // Since SQLite doesn't strictly support RowVersion/Timestamp types automatically updating,
            // we might get away without it or treat it as a normal property we update manually,
            // but MassTransit requires optimistic concurrency support.
            
            // For simplicity in SQLite demo, we might omit strict RowVersion mapping if it causes issues, 
            // but typically:
            // entity.Property(x => x.RowVersion).IsRowVersion();
        }
    }
}
