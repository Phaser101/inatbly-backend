using Intably.Domain.Processes;
using Intably.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intably.Infrastructure.Persistence.Configurations;

internal sealed class ProcessAuditEventConfiguration
    : IEntityTypeConfiguration<ProcessAuditEvent>
{
    public void Configure(EntityTypeBuilder<ProcessAuditEvent> builder)
    {
        builder.ToTable("ProcessAuditEvents");
        builder.HasKey(auditEvent => auditEvent.Id);
        builder.Property(auditEvent => auditEvent.Id).HasColumnName("aerg");
        builder
            .Property(auditEvent => auditEvent.ActorDisplayName)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(auditEvent => auditEvent.Action).HasMaxLength(100).IsRequired();
        builder
            .Property(auditEvent => auditEvent.AffectedItem)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(auditEvent => auditEvent.BeforeValue).HasMaxLength(4000);
        builder.Property(auditEvent => auditEvent.AfterValue).HasMaxLength(4000);
        builder.Property(auditEvent => auditEvent.Note).HasMaxLength(4000);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(auditEvent => auditEvent.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<ProcessStep>()
            .WithMany()
            .HasForeignKey(auditEvent => auditEvent.ProcessStepId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
