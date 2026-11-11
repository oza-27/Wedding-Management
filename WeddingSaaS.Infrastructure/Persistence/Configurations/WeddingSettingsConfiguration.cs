using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingSaaS.Domain.Entities;
using WeddingSaaS.Domain.ValueObjects;

namespace WeddingSaaS.Infrastructure.Persistence.Configurations;

public sealed class WeddingSettingsConfiguration : IEntityTypeConfiguration<WeddingSettings>
{
    public void Configure(EntityTypeBuilder<WeddingSettings> builder)
    {
        builder.ToTable("wedding_settings", "public");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.WeddingId).HasColumnName("wedding_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.TimeZoneId).HasColumnName("time_zone_id").HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("varchar(3)").HasMaxLength(3).HasConversion(x => x.Value, x => CurrencyCode.Create(x)).IsRequired();
        builder.Property(x => x.GuestPortalEnabled).HasColumnName("guest_portal_enabled").HasColumnType("boolean").HasDefaultValue(false).IsRequired();
        builder.HasOne<Wedding>().WithOne().HasForeignKey<WeddingSettings>(x => new { x.WeddingId, x.TenantId }).HasPrincipalKey<Wedding>(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_wedding_settings_weddings_wedding_tenant");
    }
}
