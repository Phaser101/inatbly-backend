using Intably.Domain.Processes;
using Intably.Domain.Templates;
using Intably.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intably.Infrastructure.Persistence.Configurations;

internal sealed class ProcessInstanceConfiguration
    : IEntityTypeConfiguration<ProcessInstance>
{
    public void Configure(EntityTypeBuilder<ProcessInstance> builder)
    {
        builder.ToTable("Processes");
        builder.HasKey(process => process.Id);
        builder.Property(process => process.Id).HasColumnName("pirg");
        builder.Property(process => process.TemplateName).HasMaxLength(200).IsRequired();
        builder.Property(process => process.Name).HasMaxLength(200).IsRequired();
        builder
            .Property(process => process.OwnerDisplayName)
            .HasMaxLength(200)
            .IsRequired();
        builder
            .Property(process => process.ClosedByDisplayName)
            .HasMaxLength(200);
        builder.Property(process => process.Status).HasConversion<string>();
        builder.Property(process => process.Context).HasMaxLength(1000);
        builder.Property(process => process.FinalNote).HasMaxLength(4000);
        builder.Property(process => process.RowVersion).IsRowVersion();

        builder
            .HasOne<ProcessTemplate>()
            .WithMany()
            .HasForeignKey(process => process.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(process => process.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(process => process.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(process => process.StepGroups)
            .WithOne()
            .HasForeignKey(group => group.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(process => process.Steps)
            .WithOne()
            .HasForeignKey(step => step.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(process => process.InformationValues)
            .WithOne()
            .HasForeignKey(value => value.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(process => process.AuditEvents)
            .WithOne()
            .HasForeignKey(auditEvent => auditEvent.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
