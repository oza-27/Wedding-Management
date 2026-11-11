using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WeddingSaaS.Domain.Entities;
using WeddingSaaS.Domain.Enums;
using WeddingSaaS.Infrastructure.Identity;
using WeddingSaaS.Infrastructure.Persistence.Context;
using Xunit.Sdk;

namespace WeddingSaaS.IntegrationTests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEDDINGSAAS_TEST_CONNECTION")))
            Skip = "Set WEDDINGSAAS_TEST_CONNECTION to a dedicated, isolated PostgreSQL database whose name includes 'test' to run database integration tests.";
    }
}

public sealed class PostgreSqlPersistenceTests
{
    [PostgreSqlFact]
    public async Task Migration_constraints_relationships_and_postgres_types_work_end_to_end()
    {
        var rawConnectionString = Environment.GetEnvironmentVariable("WEDDINGSAAS_TEST_CONNECTION")!;
        var connection = new NpgsqlConnectionStringBuilder(rawConnectionString);
        if (!string.Equals(connection.Database, "WeddingSaaS_Dev_Test", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to run PostgreSQL integration tests unless the database is exactly WeddingSaaS_Dev_Test.");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(rawConnectionString).Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync();
        Assert.True(await context.Database.CanConnectAsync());

        var roleId = Guid.Parse("10100000-0000-0000-0000-000000000001");
        var testEmail = $"integration-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = $"integration-{Guid.NewGuid():N}", NormalizedUserName = $"INTEGRATION-{Guid.NewGuid():N}",
            Email = testEmail, NormalizedEmail = testEmail.ToUpperInvariant(), EmailConfirmed = true,
            PasswordHash = "test-hash-only", SecurityStamp = Guid.NewGuid().ToString("N"), ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            FirstName = "Test", LastName = "User", IsActive = true, CreatedAt = DateTimeOffset.UtcNow
        };
        var tenant = new Tenant(Guid.NewGuid(), "Persistence Test", $"test-{Guid.NewGuid():N}", TenantType.Planner, email: "owner@example.test", phone: "+919876543210", createdAt: new DateTimeOffset(2027, 1, 1, 10, 0, 0, TimeSpan.FromHours(5.5)));
        context.Users.Add(user);
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        context.TenantMemberships.Add(new TenantMembership(Guid.NewGuid(), tenant.Id, user.Id, roleId, TenantMembershipStatus.Active));
        await context.SaveChangesAsync();
        context.TenantMemberships.Add(new TenantMembership(Guid.NewGuid(), tenant.Id, user.Id, roleId, TenantMembershipStatus.Active));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        var wedding = new Wedding(Guid.NewGuid(), tenant.Id, "Test Wedding", "Bride", "Groom", new DateOnly(2027, 5, 10), new DateOnly(2027, 5, 12));
        var otherTenant = new Tenant(Guid.NewGuid(), "Other Test", $"test-{Guid.NewGuid():N}", TenantType.Family);
        var otherWedding = new Wedding(Guid.NewGuid(), otherTenant.Id, "Other Wedding", "Bride", "Groom", new DateOnly(2027, 5, 10), new DateOnly(2027, 5, 12));
        context.AddRange(otherTenant, wedding, otherWedding);
        await context.SaveChangesAsync();

        var tenantAVenue = new Venue(Guid.NewGuid(), wedding, "Tenant A Venue");
        var tenantBVenue = new Venue(Guid.NewGuid(), otherWedding, "Tenant B Venue");
        context.Venues.AddRange(tenantAVenue, tenantBVenue);
        await context.SaveChangesAsync();
        var crossTenantVenue = new Venue(Guid.NewGuid(), otherWedding, "Cross Tenant Venue");
        context.Venues.Add(crossTenantVenue);
        context.Entry(crossTenantVenue).Property(x => x.TenantId).CurrentValue = tenant.Id;
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        var invalidFunction = new WeddingFunction(Guid.NewGuid(), wedding, "Wrong venue", wedding.StartDate);
        context.WeddingFunctions.Add(invalidFunction);
        context.Entry(invalidFunction).Property(x => x.VenueId).CurrentValue = tenantBVenue.Id;
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        var settings = new WeddingSettings(Guid.NewGuid(), wedding, "UTC", "usd");
        context.WeddingSettings.Add(settings);
        await context.SaveChangesAsync();
        context.WeddingSettings.Add(new WeddingSettings(Guid.NewGuid(), wedding, "UTC"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        var function = new WeddingFunction(Guid.NewGuid(), wedding, "Reception", new DateOnly(2027, 5, 11), new TimeOnly(18, 30), new TimeOnly(22, 0), venue: tenantAVenue);
        context.WeddingFunctions.Add(function);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var savedFunction = await context.WeddingFunctions.SingleAsync(x => x.Id == function.Id);
        var savedTenant = await context.Tenants.SingleAsync(x => x.Id == tenant.Id);
        var savedSettings = await context.WeddingSettings.SingleAsync(x => x.WeddingId == wedding.Id);
        Assert.Equal(new DateOnly(2027, 5, 11), savedFunction.FunctionDate);
        Assert.Equal(new TimeOnly(18, 30), savedFunction.StartTime);
        Assert.Equal(new TimeOnly(22, 0), savedFunction.EndTime);
        Assert.Equal("owner@example.test", savedTenant.Email?.Value);
        Assert.Equal("+919876543210", savedTenant.Phone?.Value);
        Assert.Equal(tenant.Slug.Value, savedTenant.Slug.Value);
        Assert.Equal("USD", savedSettings.CurrencyCode.Value);
        Assert.Equal(new DateTimeOffset(2027, 1, 1, 4, 30, 0, TimeSpan.Zero), savedTenant.CreatedAt);
        Assert.Equal(TimeSpan.Zero, savedTenant.CreatedAt.Offset);

        await context.WeddingFunctions.Where(x => x.TenantId == tenant.Id || x.TenantId == otherTenant.Id).ExecuteDeleteAsync();
        await context.WeddingSettings.Where(x => x.TenantId == tenant.Id || x.TenantId == otherTenant.Id).ExecuteDeleteAsync();
        await context.Venues.Where(x => x.TenantId == tenant.Id || x.TenantId == otherTenant.Id).ExecuteDeleteAsync();
        await context.Weddings.Where(x => x.TenantId == tenant.Id || x.TenantId == otherTenant.Id).ExecuteDeleteAsync();
        await context.TenantMemberships.Where(x => x.TenantId == tenant.Id || x.TenantId == otherTenant.Id).ExecuteDeleteAsync();
        await context.Tenants.Where(x => x.Id == tenant.Id || x.Id == otherTenant.Id).ExecuteDeleteAsync();
        await context.Users.Where(x => x.Id == user.Id).ExecuteDeleteAsync();
    }
}



