using Intably.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intably.Infrastructure.Persistence.Configurations;

internal sealed class TemplateRequestFieldConfiguration
    : IEntityTypeConfiguration<TemplateRequestField>
{
    public void Configure(EntityTypeBuilder<TemplateRequestField> builder)
    {
        builder.ToTable("TemplateRequestFields");
        builder.HasKey(field => field.Id);
        builder.Property(field => field.Id).HasColumnName("rfrg");
        builder.Property(field => field.Label).HasMaxLength(200).IsRequired();
        builder.Property(field => field.Type).HasConversion<string>();
        builder.Property(field => field.Placeholder).HasMaxLength(500);
        builder.Property(field => field.Source).HasConversion<string>();
        builder.Property(field => field.Kind).HasConversion<string>();

        builder
            .HasIndex(field => new { field.TemplateVersionId, field.Order })
            .IsUnique();

        builder
            .HasOne<TemplateStep>()
            .WithMany()
            .HasForeignKey(field => new
            {
                field.TemplateVersionId,
                field.ProducingTemplateStepId,
            })
            .HasPrincipalKey(step => new
            {
                step.TemplateVersionId,
                step.Id,
            })
            .OnDelete(DeleteBehavior.NoAction);

        builder
            .HasMany(field => field.Options)
            .WithOne()
            .HasForeignKey(option => option.RequestFieldId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TemplateRequestFieldOptionConfiguration
    : IEntityTypeConfiguration<TemplateRequestFieldOption>
{
    public void Configure(EntityTypeBuilder<TemplateRequestFieldOption> builder)
    {
        builder.ToTable("TemplateRequestFieldOptions");
        builder.HasKey(option => option.Id);
        builder.Property(option => option.Value).HasMaxLength(500).IsRequired();

        builder
            .HasIndex(option => new { option.RequestFieldId, option.Order })
            .IsUnique();
    }
}
