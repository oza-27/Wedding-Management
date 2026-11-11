using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingSaaS.Domain.Entities;
using WeddingSaaS.Infrastructure.Identity;

namespace WeddingSaaS.Infrastructure.Persistence.Configurations;

public sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("tenant_memberships", "public");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.RoleId).HasColumnName("role_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.MembershipStatus).HasColumnName("membership_status").HasColumnType("smallint").HasConversion<short>().IsRequired();
        builder.Property(x => x.JoinedAt).HasColumnName("joined_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique().HasDatabaseName("ux_tenant_memberships_tenant_user");
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_tenant_memberships_user_id");
        builder.HasIndex(x => new { x.TenantId, x.MembershipStatus }).HasDatabaseName("ix_tenant_memberships_tenant_status");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_tenant_memberships_asp_net_users_user_id");
        builder.HasOne<IdentityRole<Guid>>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_tenant_memberships_asp_net_roles_role_id");
    }
}
