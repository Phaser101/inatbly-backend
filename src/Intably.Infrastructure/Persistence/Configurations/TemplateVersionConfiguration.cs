using Intably.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intably.Infrastructure.Persistence.Configurations;

internal sealed class TemplateVersionConfiguration
    : IEntityTypeConfiguration<TemplateVersion>
{
    public void Configure(EntityTypeBuilder<TemplateVersion> builder)
    {
        builder.ToTable("TemplateVersions");
        builder.HasKey(version => version.Id);
        builder.Property(version => version.Name).HasMaxLength(200).IsRequired();
        builder.Property(version => version.Description).HasMaxLength(2000);

        builder
            .HasIndex(version => new { version.TemplateId, version.Version })
            .IsUnique();

        builder
            .HasMany(version => version.RequestFields)
            .WithOne()
            .HasForeignKey(field => field.TemplateVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(version => version.StepGroups)
            .WithOne()
            .HasForeignKey(group => group.TemplateVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(version => version.Steps)
            .WithOne()
            .HasForeignKey(step => step.TemplateVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
