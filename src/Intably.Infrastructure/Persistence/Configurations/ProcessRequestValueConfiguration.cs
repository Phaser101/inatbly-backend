using Intably.Domain.Processes;
using Intably.Domain.Users;
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
        builder.Property(value => value.Kind).HasConversion<string>();
        builder.Property(value => value.OptionsJson).HasColumnType("nvarchar(max)");
        builder
            .Property(value => value.ModifiedByDisplayName)
            .HasMaxLength(200);
        builder.Property(value => value.RowVersion).IsRowVersion();

        builder
            .HasIndex(value => new { value.ProcessId, value.Order })
            .IsUnique();

        builder
            .HasOne<ProcessStep>()
            .WithMany()
            .HasForeignKey(value => new
            {
                value.ProcessId,
                value.ProducingProcessStepId,
            })
            .HasPrincipalKey(step => new
            {
                step.ProcessId,
                step.Id,
            })
            .OnDelete(DeleteBehavior.NoAction);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(value => value.ModifiedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
