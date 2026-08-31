using Intably.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intably.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnName("grg");
        builder.Property(user => user.EntraTenantId).HasMaxLength(64).IsRequired();
        builder.Property(user => user.EntraObjectId).HasMaxLength(64).IsRequired();
        builder.Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(320).IsRequired();

        builder
            .HasIndex(user => new { user.EntraTenantId, user.EntraObjectId })
            .IsUnique();
        builder.HasIndex(user => user.Email);
    }
}
