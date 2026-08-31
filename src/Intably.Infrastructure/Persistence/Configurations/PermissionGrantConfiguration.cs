using Intably.Domain.Permissions;
using Intably.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intably.Infrastructure.Persistence.Configurations;

internal sealed class PermissionGrantConfiguration
    : IEntityTypeConfiguration<PermissionGrant>
{
    public void Configure(EntityTypeBuilder<PermissionGrant> builder)
    {
        builder.ToTable("PermissionGrants");
        builder.HasKey(grant => grant.Id);
        builder.Property(grant => grant.Id).HasColumnName("pgrg");
        builder.Property(grant => grant.Permission).HasConversion<string>();

        builder
            .HasIndex(grant => new { grant.UserId, grant.Permission })
            .IsUnique();

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(grant => grant.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(grant => grant.GrantedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
