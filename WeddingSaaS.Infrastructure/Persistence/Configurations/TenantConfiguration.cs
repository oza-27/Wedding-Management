using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingSaaS.Domain.Entities;
using WeddingSaaS.Domain.ValueObjects;

namespace WeddingSaaS.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", "public");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.Name).HasColumnName("name").HasColumnType("varchar(200)").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasColumnType("varchar(63)").HasMaxLength(63).HasConversion(x => x.Value, x => TenantSlug.Create(x)).IsRequired();
        builder.Property(x => x.TenantType).HasColumnName("tenant_type").HasColumnType("smallint").HasConversion<short>().IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasColumnType("varchar(254)").HasMaxLength(254).HasConversion(x => x == null ? null : x.Value, x => x == null ? null : EmailAddress.Create(x));
        builder.Property(x => x.Phone).HasColumnName("phone").HasColumnType("varchar(16)").HasMaxLength(16).HasConversion(x => x == null ? null : x.Value, x => x == null ? null : PhoneNumber.Create(x));
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("boolean").HasDefaultValue(true).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("ux_tenants_slug");
        builder.HasIndex(x => x.IsActive).HasDatabaseName("ix_tenants_is_active");
        builder.HasMany(x => x.Memberships).WithOne().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_tenant_memberships_tenants_tenant_id");
        builder.Navigation(x => x.Memberships).HasField("_memberships").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
