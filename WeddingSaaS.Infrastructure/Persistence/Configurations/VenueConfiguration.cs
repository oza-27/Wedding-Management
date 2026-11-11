using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingSaaS.Domain.Entities;
using WeddingSaaS.Domain.ValueObjects;

namespace WeddingSaaS.Infrastructure.Persistence.Configurations;

public sealed class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.ToTable("venues", "public", table =>
        {
            table.HasCheckConstraint("ck_venues_latitude_range", "latitude IS NULL OR (latitude >= -90 AND latitude <= 90)");
            table.HasCheckConstraint("ck_venues_longitude_range", "longitude IS NULL OR (longitude >= -180 AND longitude <= 180)");
            table.HasCheckConstraint("ck_venues_coordinate_pair", "(latitude IS NULL) = (longitude IS NULL)");
        });
        builder.ConfigureBaseEntity();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.WeddingId).HasColumnName("wedding_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasColumnType("varchar(200)").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Address).HasColumnName("address").HasColumnType("varchar(500)").HasMaxLength(500);
        builder.Property(x => x.Latitude).HasColumnName("latitude").HasColumnType("numeric(9,6)");
        builder.Property(x => x.Longitude).HasColumnName("longitude").HasColumnType("numeric(10,6)");
        builder.Property(x => x.ContactPerson).HasColumnName("contact_person").HasColumnType("varchar(150)").HasMaxLength(150);
        builder.Property(x => x.ContactNumber).HasColumnName("contact_number").HasColumnType("varchar(16)").HasMaxLength(16).HasConversion(x => x == null ? null : x.Value, x => x == null ? null : PhoneNumber.Create(x));
        builder.HasAlternateKey(x => new { x.Id, x.TenantId, x.WeddingId }).HasName("ak_venues_id_tenant_wedding");
        builder.HasOne<Wedding>().WithMany().HasForeignKey(x => new { x.WeddingId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_venues_weddings_wedding_tenant");
        builder.HasIndex(x => new { x.TenantId, x.WeddingId }).HasDatabaseName("ix_venues_tenant_wedding");
    }
}

