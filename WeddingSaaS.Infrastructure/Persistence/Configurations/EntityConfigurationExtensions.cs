using Microsoft.EntityFrameworkCore;
using WeddingSaaS.Domain.Common;

namespace WeddingSaaS.Infrastructure.Persistence.Configurations;

internal static class EntityConfigurationExtensions
{
    public static void ConfigureBaseEntity<TEntity>(this Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder)
        where TEntity : BaseEntity
    {
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(entity => entity.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
        builder.Property(entity => entity.IsDeleted).HasColumnName("is_deleted").HasColumnType("boolean").HasDefaultValue(false).IsRequired();
        builder.HasQueryFilter(entity => !entity.IsDeleted);
    }
}
