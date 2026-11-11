using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WeddingSaaS.Infrastructure.Persistence.Context;

/// <summary>Design-time factory reads the standard ASP.NET connection-string environment variable; credentials never live in source.</summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("WEDDINGSAAS_DESIGNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Set ConnectionStrings__DefaultConnection (or WEDDINGSAAS_DESIGNTIME_CONNECTION) to a local development connection string before running EF migration commands.");
        if (connectionString.Contains("SET_VIA_USER_SECRETS", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The configured connection string is a placeholder. Supply a real local development connection string through environment variables.");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options;
        return new ApplicationDbContext(options);
    }
}
