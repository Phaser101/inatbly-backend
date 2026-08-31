using Intably.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intably.Infrastructure.Persistence.Configurations;

internal sealed class FunctionalRoleConfiguration
    : IEntityTypeConfiguration<FunctionalRole>
{
    public void Configure(EntityTypeBuilder<FunctionalRole> builder)
    {
        builder.ToTable("FunctionalRoles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).HasColumnName("frrg");
        builder.Property(role => role.Name).HasMaxLength(100).IsRequired();
        builder.Property(role => role.Description).HasMaxLength(500).IsRequired();

        builder.HasIndex(role => role.Name).IsUnique();
    }
}

internal sealed class UserFunctionalRoleConfiguration
    : IEntityTypeConfiguration<UserFunctionalRole>
{
    public void Configure(EntityTypeBuilder<UserFunctionalRole> builder)
    {
        builder.ToTable("UserFunctionalRoles");
        builder.HasKey(item => new { item.UserId, item.FunctionalRoleId });

        builder
            .HasOne<Intably.Domain.Users.User>()
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<FunctionalRole>()
            .WithMany()
            .HasForeignKey(item => item.FunctionalRoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
