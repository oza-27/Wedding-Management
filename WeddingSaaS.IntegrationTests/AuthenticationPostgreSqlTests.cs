using System.Text.RegularExpressions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using WeddingSaaS.Application.Abstractions.Authentication;
using WeddingSaaS.Application.Abstractions.Email;
using WeddingSaaS.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WeddingSaaS.Domain.Entities;
using WeddingSaaS.Domain.Enums;
using WeddingSaaS.Infrastructure.Identity;
using WeddingSaaS.Infrastructure.Persistence.Context;
using WeddingSaaS.Infrastructure.Security;
using WeddingSaaS.Infrastructure.Services;

namespace WeddingSaaS.IntegrationTests;

public sealed class AuthenticationPostgreSqlTests
{
    [PostgreSqlFact]
    public async Task Identity_registration_verification_login_refresh_reset_and_tenant_switching_work()
    {
        var connectionString = Environment.GetEnvironmentVariable("WEDDINGSAAS_TEST_CONNECTION")!;
        var parsed = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(parsed.Database, "WeddingSaaS_Dev_Test", StringComparison.Ordinal) || parsed.Host is not ("127.0.0.1" or "localhost" or "::1"))
            throw new InvalidOperationException("Authentication integration tests require a loopback connection to exactly WeddingSaaS_Dev_Test.");

        var emailCapture = new CaptureEmailService();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddSingleton<IEmailService>(emailCapture);
        var jwt = new JwtOptions { Issuer = "WeddingSaaS.IntegrationTests", Audience = "WeddingSaaS.IntegrationTests.Client", SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)), AccessTokenMinutes = 15, RefreshTokenDays = 30 };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Email:FrontendBaseUrl"] = "http://localhost:4200", ["Jwt:RefreshTokenDays"] = "30" }).Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(connectionString));
        services.AddIdentityCore<ApplicationUser>(o =>
        {
            o.SignIn.RequireConfirmedEmail = true;
            o.Lockout.AllowedForNewUsers = true;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            o.Password.RequiredLength = 12;
            o.Password.RequireDigit = true;
            o.Password.RequireUppercase = true;
            o.Password.RequireLowercase = true;
            o.Password.RequireNonAlphanumeric = true;
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        services.AddSingleton<ITokenService>(_ => new TokenService(Options.Create(jwt)));
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        Assert.Equal("WeddingSaaS_Dev_Test", (await db.Database.SqlQueryRaw<string>("select current_database() as \"Value\"").SingleAsync()));
        var auth = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var email = $"auth-{Guid.NewGuid():N}@example.test";
        const string password = "MySecure!Password2026";
        Guid userId = Guid.Empty;
        Guid firstTenantId = Guid.Empty;
        Guid secondTenantId = Guid.Empty;
        try
        {
            var registration = await auth.RegisterAsync(new("Test", "User", email, null, password, password, $"Auth Test Workspace {Guid.NewGuid():N}", TenantType.Planner), default);
            userId = registration.UserId;
            firstTenantId = await db.TenantMemberships.Where(x => x.UserId == userId).Select(x => x.TenantId).SingleAsync();
            var ownerMembership = await db.TenantMemberships.SingleAsync(x => x.UserId == userId && x.TenantId == firstTenantId);
            Assert.Equal(Guid.Parse("10100000-0000-0000-0000-000000000001"), ownerMembership.RoleId);
            var identityManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var identityUser = await identityManager.FindByIdAsync(userId.ToString());
            Assert.NotNull(identityUser);
            Assert.Empty(await identityManager.GetRolesAsync(identityUser!));
            Assert.Null(await auth.LoginAsync(new(email, password), "127.0.0.1", default));
            Assert.False(await auth.VerifyEmailAsync(new(email, "not-a-token"), default));

            var verificationToken = GetToken(emailCapture.Messages.Last().HtmlBody);
            Assert.True(await auth.VerifyEmailAsync(new(email, verificationToken), default));
            Assert.False(await auth.VerifyEmailAsync(new(email, verificationToken), default));
            await Assert.ThrowsAsync<DuplicateEmailException>(() => auth.RegisterAsync(new("Other", "User", email, null, password, password, "Other Workspace", TenantType.Family), default));

            var incorrect = await auth.LoginAsync(new(email, "Wrong!Password123"), "127.0.0.1", default);
            Assert.Null(incorrect);
            var pair = await auth.LoginAsync(new(email, password), "127.0.0.1", default);
            Assert.NotNull(pair);
            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            var storedSession = await db.RefreshTokens.SingleAsync(x => x.UserId == userId && x.TokenHash == tokenService.HashRefreshToken(pair!.RefreshToken));
            Assert.Equal(tokenService.HashRefreshToken(pair.RefreshToken), storedSession.TokenHash);
            Assert.NotEqual(pair.RefreshToken, storedSession.TokenHash);
            var concurrentRotations = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            {
                await using var contender = provider.CreateAsyncScope();
                return await contender.ServiceProvider.GetRequiredService<IAuthenticationService>().RefreshAsync(pair!.RefreshToken, "127.0.0.1", default);
            }));
            Assert.InRange(concurrentRotations.Count(x => x is not null), 0, 1);
            var issuedByRace = concurrentRotations.SingleOrDefault(x => x is not null);
            Assert.Null(await auth.RefreshAsync(pair!.RefreshToken, "127.0.0.1", default));
            if (issuedByRace is not null) Assert.Null(await auth.RefreshAsync(issuedByRace.RefreshToken, "127.0.0.1", default));

            var profile = await auth.GetProfileAsync(userId, default);
            Assert.True(profile?.IsEmailVerified);
            var controller = new AuthController(auth) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString())], "integration")) } } };
            var currentUserResponse = await controller.Me(default);
            Assert.IsType<OkObjectResult>(currentUserResponse);
            var unknownForgot = await controller.ForgotPassword(new EmailDto("absent@example.test"), default);
            Assert.IsType<OkObjectResult>(unknownForgot);
            var knownForgot = await controller.ForgotPassword(new EmailDto(email), default);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(((OkObjectResult)unknownForgot).Value), System.Text.Json.JsonSerializer.Serialize(((OkObjectResult)knownForgot).Value));
            var ownTenants = await auth.GetTenantsAsync(userId, default);
            Assert.Single(ownTenants);
            var tenant = await db.Tenants.SingleAsync(x => x.Id == ownTenants[0].TenantId);
            firstTenantId = tenant.Id;
            var second = new Tenant(Guid.NewGuid(), "Second Auth Workspace", $"second-auth-{Guid.NewGuid():N}", TenantType.Family);
            secondTenantId = second.Id;
            db.Tenants.Add(second);
            db.TenantMemberships.Add(new TenantMembership(Guid.NewGuid(), second.Id, userId, Guid.Parse("10100000-0000-0000-0000-000000000001"), TenantMembershipStatus.Active));
            await db.SaveChangesAsync();
            Assert.Equal(2, (await auth.GetTenantsAsync(userId, default)).Count);
            Assert.Null(await auth.SwitchTenantAsync(userId, Guid.NewGuid(), "127.0.0.1", default));
            var switched = await auth.SwitchTenantAsync(userId, second.Id, "127.0.0.1", default);
            Assert.NotNull(switched);
            var switchedJwt = new JwtSecurityTokenHandler().ReadJwtToken(switched!.AccessToken);
            Assert.Equal(second.Id.ToString(), switchedJwt.Claims.Single(x => x.Type == "tenant_id").Value);
            db.TenantMemberships.Where(x => x.TenantId == second.Id && x.UserId == userId).ExecuteUpdate(s => s.SetProperty(x => x.MembershipStatus, TenantMembershipStatus.Suspended));
            Assert.Null(await auth.SwitchTenantAsync(userId, second.Id, "127.0.0.1", default));
            Assert.DoesNotContain((await auth.GetTenantsAsync(userId, default)), x => x.TenantId == second.Id);
            Assert.Null(await auth.RefreshAsync(switched.RefreshToken, "127.0.0.1", default));

            var session = await auth.LoginAsync(new(email, password), "127.0.0.1", default);
            await auth.LogoutAsync(session!.RefreshToken, default);
            Assert.Null(await auth.RefreshAsync(session.RefreshToken, "127.0.0.1", default));

            var beforeReset = await auth.LoginAsync(new(email, password), "127.0.0.1", default);
            await auth.ForgotPasswordAsync(email, default);
            var resetToken = GetToken(emailCapture.Messages.Last().HtmlBody);
            const string newPassword = "BrandNew!Password2027";
            Assert.True(await auth.ResetPasswordAsync(new(email, resetToken, newPassword, newPassword), default));
            Assert.Null(await auth.RefreshAsync(beforeReset!.RefreshToken, "127.0.0.1", default));
            Assert.Null(await auth.LoginAsync(new(email, password), "127.0.0.1", default));
            Assert.NotNull(await auth.LoginAsync(new(email, newPassword), "127.0.0.1", default));

            var sessionA = await auth.LoginAsync(new(email, newPassword), "127.0.0.1", default);
            var sessionB = await auth.LoginAsync(new(email, newPassword), "127.0.0.1", default);
            await auth.LogoutAllAsync(userId, default);
            Assert.Null(await auth.RefreshAsync(sessionA!.RefreshToken, "127.0.0.1", default));
            Assert.Null(await auth.RefreshAsync(sessionB!.RefreshToken, "127.0.0.1", default));

            var expiredRaw = tokenService.CreateRefreshToken();
            var expiredAt = DateTimeOffset.UtcNow.AddDays(-1);
            db.RefreshTokens.Add(new RefreshToken(Guid.NewGuid(), userId, tokenService.HashRefreshToken(expiredRaw), expiredAt.AddDays(-1), expiredAt, "127.0.0.1"));
            await db.SaveChangesAsync();
            Assert.Null(await auth.RefreshAsync(expiredRaw, "127.0.0.1", default));

            for (var attempt = 0; attempt < 5; attempt++) Assert.Null(await auth.LoginAsync(new(email, "Wrong!Password123"), "127.0.0.1", default));
            Assert.Null(await auth.LoginAsync(new(email, newPassword), "127.0.0.1", default));
        }
        finally
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from public.tenant_memberships where user_id = {userId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from public.tenants where id in ({firstTenantId}, {secondTenantId})");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from public.asp_net_users where id = {userId}");
        }
    }

    private static string GetToken(string html)
    {
        var match = Regex.Match(html, "token=([^&\\\"]+)");
        Assert.True(match.Success, "Captured email must contain its purpose token link.");
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    private sealed class CaptureEmailService : IEmailService
    {
        public List<EmailMessage> Messages { get; } = [];
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) { Messages.Add(message); return Task.CompletedTask; }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "WeddingSaaS.IntegrationTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
