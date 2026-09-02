using Intably.Domain.Roles;
using Intably.Domain.Templates;
using Intably.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intably.Infrastructure.Persistence.Configurations;

internal sealed class TemplateStepConfiguration
    : IEntityTypeConfiguration<TemplateStep>
{
    public void Configure(EntityTypeBuilder<TemplateStep> builder)
    {
        builder.ToTable("TemplateSteps");
        builder.HasKey(step => step.Id);
        builder.HasAlternateKey(step => new
        {
            step.TemplateVersionId,
            step.Id,
        });
        builder.Property(step => step.Id).HasColumnName("ptsrg");
        builder.Property(step => step.Title).HasMaxLength(200).IsRequired();
        builder.Property(step => step.RequiredRoleName).HasMaxLength(200);
        builder.Property(step => step.Instructions).HasMaxLength(4000);
        builder.Property(step => step.SupportingUrl).HasMaxLength(2048);
        builder.Property(step => step.DefaultAssigneeName).HasMaxLength(200);

        builder
            .HasIndex(step => new { step.TemplateStepGroupId, step.Order })
            .IsUnique();

        builder
            .HasOne<TemplateStepGroup>()
            .WithMany()
            .HasForeignKey(step => new
            {
                step.TemplateVersionId,
                step.TemplateStepGroupId,
            })
            .HasPrincipalKey(group => new
            {
                group.TemplateVersionId,
                group.Id,
            })
            .OnDelete(DeleteBehavior.NoAction);

        builder
            .HasOne<FunctionalRole>()
            .WithMany()
            .HasForeignKey(step => step.RequiredRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(step => step.DefaultAssigneeUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
