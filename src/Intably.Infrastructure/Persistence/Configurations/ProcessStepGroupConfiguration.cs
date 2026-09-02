using Intably.Domain.Processes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intably.Infrastructure.Persistence.Configurations;

internal sealed class ProcessStepGroupConfiguration
    : IEntityTypeConfiguration<ProcessStepGroup>
{
    public void Configure(EntityTypeBuilder<ProcessStepGroup> builder)
    {
        builder.ToTable("ProcessStepGroups");
        builder.HasKey(group => group.Id);
        builder.HasAlternateKey(group => new
        {
            group.ProcessId,
            group.Id,
        });
        builder.Property(group => group.Id).HasColumnName("psgrg");
        builder.Property(group => group.Name).HasMaxLength(200).IsRequired();
        builder.Property(group => group.Description).HasMaxLength(2000);
        builder.Property(group => group.ExecutionMode).HasConversion<string>();
        builder
            .HasIndex(group => new { group.ProcessId, group.Order })
            .IsUnique();
        builder
            .HasIndex(group => new { group.ProcessId, group.SourceTemplateStepGroupId })
            .IsUnique();

        builder
            .HasMany(group => group.PrerequisiteGroups)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "ProcessStepGroupPrerequisite",
                right => right
                    .HasOne<ProcessStepGroup>()
                    .WithMany()
                    .HasForeignKey("PrerequisiteProcessStepGroupId")
                    .OnDelete(DeleteBehavior.NoAction),
                left => left
                    .HasOne<ProcessStepGroup>()
                    .WithMany()
                    .HasForeignKey("ProcessStepGroupId")
                    .OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("ProcessStepGroupPrerequisites");
                    join.HasKey(
                        "ProcessStepGroupId",
                        "PrerequisiteProcessStepGroupId");
                });
    }
}
