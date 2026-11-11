using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingSaaS.Infrastructure.Identity;

namespace WeddingSaaS.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", "public");
        builder.HasKey(x => x.Id).HasName("pk_refresh_tokens");
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.ReplacedByTokenHash).HasColumnName("replaced_by_token_hash").HasColumnType("varchar(64)").HasMaxLength(64);
        builder.Property(x => x.CreatedByIp).HasColumnName("created_by_ip").HasColumnType("varchar(64)").HasMaxLength(64);
        builder.Property(x => x.RevocationReason).HasColumnName("revocation_reason").HasColumnType("varchar(100)").HasMaxLength(100);
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_token_hash");
        builder.HasIndex(x => new { x.UserId, x.RevokedAtUtc, x.ExpiresAtUtc }).HasDatabaseName("ix_refresh_tokens_user_active_expiry");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_refresh_tokens_asp_net_users_user_id");
    }
}
