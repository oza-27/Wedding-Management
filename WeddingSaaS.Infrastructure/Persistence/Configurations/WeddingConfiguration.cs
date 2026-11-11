using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingSaaS.Domain.Entities;

namespace WeddingSaaS.Infrastructure.Persistence.Configurations;

public sealed class WeddingConfiguration : IEntityTypeConfiguration<Wedding>
{
    public void Configure(EntityTypeBuilder<Wedding> builder)
    {
        builder.ToTable("weddings", "public");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.WeddingName).HasColumnName("wedding_name").HasColumnType("varchar(200)").HasMaxLength(200).IsRequired();
        builder.Property(x => x.BrideName).HasColumnName("bride_name").HasColumnType("varchar(150)").HasMaxLength(150).IsRequired();
        builder.Property(x => x.GroomName).HasColumnName("groom_name").HasColumnType("varchar(150)").HasMaxLength(150).IsRequired();
        builder.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.EndDate).HasColumnName("end_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasColumnType("smallint").HasConversion<short>().IsRequired();
        builder.HasAlternateKey(x => new { x.Id, x.TenantId }).HasName("ak_weddings_id_tenant_id");
        builder.HasOne<WeddingSaaS.Domain.Entities.Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_weddings_tenants_tenant_id");
        builder.HasMany<WeddingFunction>().WithOne().HasForeignKey(x => new { x.WeddingId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_wedding_functions_weddings_wedding_tenant");
        builder.HasMany<Venue>().WithOne().HasForeignKey(x => new { x.WeddingId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_venues_weddings_wedding_tenant");
        builder.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("ix_weddings_tenant_status");
        builder.HasIndex(x => new { x.TenantId, x.StartDate }).HasDatabaseName("ix_weddings_tenant_start_date");
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt }).HasDatabaseName("ix_weddings_tenant_created_at");
    }
}

