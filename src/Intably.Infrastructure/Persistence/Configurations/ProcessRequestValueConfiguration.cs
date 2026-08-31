using Intably.Domain.Processes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intably.Infrastructure.Persistence.Configurations;

internal sealed class ProcessRequestValueConfiguration
    : IEntityTypeConfiguration<ProcessRequestValue>
{
    public void Configure(EntityTypeBuilder<ProcessRequestValue> builder)
    {
        builder.ToTable("ProcessRequestValues");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Label).HasMaxLength(200).IsRequired();
        builder.Property(value => value.FieldType).HasMaxLength(50).IsRequired();
        builder.Property(value => value.Value).HasMaxLength(4000);

        builder
            .HasIndex(value => new { value.ProcessId, value.Order })
            .IsUnique();
    }
}
