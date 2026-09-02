using Intably.Domain.Processes;
using Intably.Domain.Roles;
using Intably.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intably.Infrastructure.Persistence.Configurations;

internal sealed class ProcessStepConfiguration
    : IEntityTypeConfiguration<ProcessStep>
{
    public void Configure(EntityTypeBuilder<ProcessStep> builder)
    {
        builder.ToTable("ProcessSteps");
        builder.HasKey(step => step.Id);
        builder.HasAlternateKey(step => new
        {
            step.ProcessId,
            step.Id,
        });
        builder.Property(step => step.Id).HasColumnName("psrg");
        builder.Property(step => step.Title).HasMaxLength(200).IsRequired();
        builder
            .Property(step => step.RequiredRoleName)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(step => step.AssigneeDisplayName).HasMaxLength(200);
        builder.Property(step => step.ExecutorDisplayName).HasMaxLength(200);
        builder.Property(step => step.Instructions).HasMaxLength(4000);
        builder.Property(step => step.SupportingUrl).HasMaxLength(2048);
        builder.Property(step => step.Status).HasConversion<string>();
        builder.Property(step => step.ExecutionNote).HasMaxLength(4000);
        builder.Property(step => step.BlockedReason).HasMaxLength(4000);
        builder.Property(step => step.RowVersion).IsRowVersion();

        builder
            .HasIndex(step => new { step.ProcessId, step.SourceTemplateStepId })
            .IsUnique();
        builder
            .HasIndex(step => new { step.ProcessStepGroupId, step.Order })
            .IsUnique();

        builder
            .HasOne<ProcessStepGroup>()
            .WithMany()
            .HasForeignKey(step => new
            {
                step.ProcessId,
                step.ProcessStepGroupId,
            })
            .HasPrincipalKey(group => new
            {
                group.ProcessId,
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
            .HasForeignKey(step => step.AssigneeUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(step => step.ExecutorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
