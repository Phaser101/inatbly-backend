using Intably.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intably.Infrastructure.Persistence.Configurations;

internal sealed class ProcessTemplateConfiguration
    : IEntityTypeConfiguration<ProcessTemplate>
{
    public void Configure(EntityTypeBuilder<ProcessTemplate> builder)
    {
        builder.ToTable("ProcessTemplates");
        builder.HasKey(template => template.Id);
        builder.Property(template => template.Id).HasColumnName("ptrg");
        builder.Property(template => template.Name).HasMaxLength(200).IsRequired();
        builder.Property(template => template.Description).HasMaxLength(2000);
        builder.Property(template => template.Status).HasConversion<string>();

        builder
            .HasOne<Intably.Domain.Users.User>()
            .WithMany()
            .HasForeignKey(template => template.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(template => template.Versions)
            .WithOne()
            .HasForeignKey(version => version.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
