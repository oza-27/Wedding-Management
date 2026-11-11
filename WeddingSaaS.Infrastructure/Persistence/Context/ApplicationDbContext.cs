using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using WeddingSaaS.Application.Abstractions.Persistence;
using WeddingSaaS.Domain.Entities;
using WeddingSaaS.Infrastructure.Identity;
using System.Text.RegularExpressions;

namespace WeddingSaaS.Infrastructure.Persistence.Context;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IApplicationDbContext
{
    private static readonly IdentityRole<Guid>[] SeedRoles =
    [
        new() { Id = Guid.Parse("10100000-0000-0000-0000-000000000001"), Name = "TenantAdmin", NormalizedName = "TENANTADMIN", ConcurrencyStamp = "10100000-0000-0000-0000-000000000001" },
        new() { Id = Guid.Parse("10100000-0000-0000-0000-000000000002"), Name = "WeddingPlanner", NormalizedName = "WEDDINGPLANNER", ConcurrencyStamp = "10100000-0000-0000-0000-000000000002" },
        new() { Id = Guid.Parse("10100000-0000-0000-0000-000000000003"), Name = "FamilyMember", NormalizedName = "FAMILYMEMBER", ConcurrencyStamp = "10100000-0000-0000-0000-000000000003" },
        new() { Id = Guid.Parse("10100000-0000-0000-0000-000000000004"), Name = "Coordinator", NormalizedName = "COORDINATOR", ConcurrencyStamp = "10100000-0000-0000-0000-000000000004" }
    ];

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<TenantInvitation> TenantInvitations => Set<TenantInvitation>();
    public DbSet<Wedding> Weddings => Set<Wedding>();
    public DbSet<WeddingSettings> WeddingSettings => Set<WeddingSettings>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<WeddingFunction> WeddingFunctions => Set<WeddingFunction>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        ConfigureIdentityTables(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        builder.Entity<IdentityRole<Guid>>().HasData(SeedRoles);
        ApplySnakeCaseDatabaseNames(builder);
    }

    private static void ConfigureIdentityTables(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>().ToTable("asp_net_users", "public").HasKey(x => x.Id);
        builder.Entity<IdentityRole<Guid>>().ToTable("asp_net_roles", "public").HasKey(x => x.Id);
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("asp_net_user_claims", "public").HasKey(x => x.Id);
        builder.Entity<IdentityUserRole<Guid>>().ToTable("asp_net_user_roles", "public").HasKey(x => new { x.UserId, x.RoleId });
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("asp_net_user_logins", "public").HasKey(x => new { x.LoginProvider, x.ProviderKey });
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("asp_net_role_claims", "public").HasKey(x => x.Id);
        builder.Entity<IdentityUserToken<Guid>>().ToTable("asp_net_user_tokens", "public").HasKey(x => new { x.UserId, x.LoginProvider, x.Name });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FirstName).HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            entity.Property(x => x.IsActive).HasColumnType("boolean").HasDefaultValue(true).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
            entity.HasIndex(x => x.NormalizedEmail).IsUnique().HasDatabaseName("ux_asp_net_users_normalized_email");
        });
    }

    private static void ApplySnakeCaseDatabaseNames(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
                property.SetColumnName(ToSnakeCase(property.Name));
            foreach (var key in entityType.GetKeys())
                if (key.GetName() is { } keyName) key.SetName(ToSnakeCase(keyName));
            foreach (var foreignKey in entityType.GetForeignKeys())
                if (foreignKey.GetConstraintName() is { } foreignKeyName) foreignKey.SetConstraintName(ToSnakeCase(foreignKeyName));
            foreach (var index in entityType.GetIndexes())
                if (index.GetDatabaseName() is { } indexName) index.SetDatabaseName(ToSnakeCase(indexName));
        }
    }

    private static string ToSnakeCase(string name) => Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
}
