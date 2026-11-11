using Microsoft.EntityFrameworkCore;
using WeddingSaaS.Domain.Entities;

namespace WeddingSaaS.Application.Abstractions.Persistence;

/// <summary>Persistence contract for application handlers; implemented by Infrastructure.</summary>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<TenantMembership> TenantMemberships { get; }
    DbSet<TenantInvitation> TenantInvitations { get; }
    DbSet<Wedding> Weddings { get; }
    DbSet<WeddingSettings> WeddingSettings { get; }
    DbSet<Venue> Venues { get; }
    DbSet<WeddingFunction> WeddingFunctions { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
