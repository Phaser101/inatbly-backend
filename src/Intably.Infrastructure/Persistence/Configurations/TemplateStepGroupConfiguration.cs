using Intably.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intably.Infrastructure.Persistence.Configurations;

internal sealed class TemplateStepGroupConfiguration
    : IEntityTypeConfiguration<TemplateStepGroup>
{
    public void Configure(EntityTypeBuilder<TemplateStepGroup> builder)
    {
        builder.ToTable("TemplateStepGroups");
        builder.HasKey(group => group.Id);
        builder.HasAlternateKey(group => new
        {
            group.TemplateVersionId,
            group.Id,
        });
        builder.Property(group => group.Id).HasColumnName("ptsgrg");
        builder.Property(group => group.Name).HasMaxLength(200).IsRequired();
        builder.Property(group => group.Description).HasMaxLength(2000);
        builder.Property(group => group.ExecutionMode).HasConversion<string>();
        builder
            .HasIndex(group => new { group.TemplateVersionId, group.Order })
            .IsUnique();
        builder
            .HasMany(group => group.PrerequisiteGroups)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "TemplateStepGroupPrerequisite",
                right => right
                    .HasOne<TemplateStepGroup>()
                    .WithMany()
                    .HasForeignKey("PrerequisiteTemplateStepGroupId")
                    .OnDelete(DeleteBehavior.NoAction),
                left => left
                    .HasOne<TemplateStepGroup>()
                    .WithMany()
                    .HasForeignKey("TemplateStepGroupId")
                    .OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("TemplateStepGroupPrerequisites");
                    join.HasKey(
                        "TemplateStepGroupId",
                        "PrerequisiteTemplateStepGroupId");
                });
    }
}
