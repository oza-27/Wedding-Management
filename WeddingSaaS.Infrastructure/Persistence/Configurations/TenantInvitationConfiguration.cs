using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingSaaS.Domain.Entities;
using WeddingSaaS.Domain.ValueObjects;
using WeddingSaaS.Infrastructure.Identity;

namespace WeddingSaaS.Infrastructure.Persistence.Configurations;

public sealed class TenantInvitationConfiguration : IEntityTypeConfiguration<TenantInvitation>
{
    public void Configure(EntityTypeBuilder<TenantInvitation> builder)
    {
        builder.ToTable("tenant_invitations", "public");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasColumnType("varchar(254)").HasMaxLength(254).HasConversion(x => x.Value, x => EmailAddress.Create(x)).IsRequired();
        builder.Property(x => x.RoleId).HasColumnName("role_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.InvitationTokenHash).HasColumnName("invitation_token_hash").HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasColumnType("smallint").HasConversion<short>().IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.InvitedBy).HasColumnName("invited_by").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.AcceptedAt).HasColumnName("accepted_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.InvitationTokenHash).IsUnique().HasDatabaseName("ux_tenant_invitations_token_hash");
        builder.HasIndex(x => new { x.TenantId, x.Email }).HasDatabaseName("ix_tenant_invitations_tenant_email");
        builder.HasIndex(x => new { x.ExpiresAt, x.Status }).HasDatabaseName("ix_tenant_invitations_expires_status");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_tenant_invitations_status");
        builder.HasOne<WeddingSaaS.Domain.Entities.Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_tenant_invitations_tenants_tenant_id");
        builder.HasOne<IdentityRole<Guid>>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_tenant_invitations_asp_net_roles_role_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.InvitedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_tenant_invitations_asp_net_users_invited_by");
    }
}

