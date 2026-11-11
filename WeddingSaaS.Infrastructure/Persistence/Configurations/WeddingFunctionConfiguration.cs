using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingSaaS.Domain.Entities;

namespace WeddingSaaS.Infrastructure.Persistence.Configurations;

public sealed class WeddingFunctionConfiguration : IEntityTypeConfiguration<WeddingFunction>
{
    public void Configure(EntityTypeBuilder<WeddingFunction> builder)
    {
        builder.ToTable("wedding_functions", "public");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.WeddingId).HasColumnName("wedding_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasColumnType("varchar(200)").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(x => x.FunctionDate).HasColumnName("function_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.StartTime).HasColumnName("start_time").HasColumnType("time without time zone");
        builder.Property(x => x.EndTime).HasColumnName("end_time").HasColumnType("time without time zone");
        builder.Property(x => x.VenueId).HasColumnName("venue_id").HasColumnType("uuid");
        builder.Property(x => x.Status).HasColumnName("status").HasColumnType("smallint").HasConversion<short>().IsRequired();
        builder.HasOne<Wedding>().WithMany().HasForeignKey(x => new { x.WeddingId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_wedding_functions_weddings_wedding_tenant");
        builder.HasOne<Venue>().WithMany().HasForeignKey(x => new { x.VenueId, x.TenantId, x.WeddingId }).HasPrincipalKey(x => new { x.Id, x.TenantId, x.WeddingId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_wedding_functions_venues_venue_tenant_wedding");
        builder.HasIndex(x => new { x.TenantId, x.WeddingId, x.FunctionDate }).HasDatabaseName("ix_wedding_functions_tenant_wedding_date");
        builder.HasIndex(x => new { x.WeddingId, x.Status }).HasDatabaseName("ix_wedding_functions_wedding_status");
    }
}

