using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using WeddingSaaS.Application.Abstractions.Email;
using WeddingSaaS.Infrastructure.Persistence.Context;

namespace WeddingSaaS.IntegrationTests;

public sealed class AuthenticationApiPostgreSqlTests
{
    [PostgreSqlFact]
    public async Task Authentication_endpoints_work_through_the_real_api_pipeline()
    {
        var connectionString = Environment.GetEnvironmentVariable("WEDDINGSAAS_TEST_CONNECTION")!;
        var parsed = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(parsed.Database, "WeddingSaaS_Dev_Test", StringComparison.Ordinal) || parsed.Host is not ("127.0.0.1" or "localhost" or "::1"))
            throw new InvalidOperationException("Authentication API tests require a loopback connection to exactly WeddingSaaS_Dev_Test.");

        var signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        using var testConfiguration = new TemporaryEnvironment(new Dictionary<string, string?>
        {
            ["ConnectionStrings__DefaultConnection"] = connectionString,
            ["Jwt__Issuer"] = "WeddingSaaS.IntegrationTests", ["Jwt__Audience"] = "WeddingSaaS.IntegrationTests.Client",
            ["Jwt__SigningKey"] = signingKey, ["Jwt__AccessTokenMinutes"] = "15", ["Jwt__RefreshTokenDays"] = "30",
            ["Email__FrontendBaseUrl"] = "http://localhost:4200"
        });
        var capturedEmail = new CapturingEmailService();
        await using var factory = new AuthenticationApiFactory(connectionString, capturedEmail, signingKey);
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();
            Assert.Equal("WeddingSaaS_Dev_Test", await db.Database.SqlQueryRaw<string>("select current_database() as \"Value\"").SingleAsync());
        }

        var swagger = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        foreach (var route in new[] { "register", "verify-email", "resend-verification", "login", "refresh", "logout", "logout-all", "forgot-password", "reset-password", "me", "tenants", "switch-tenant" })
            Assert.Contains($"/api/v1/auth/{route}", swagger.GetProperty("paths").EnumerateObject().Select(x => x.Name));
        Assert.True(swagger.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));

        var email = $"api-auth-{Guid.NewGuid():N}@example.test";
        const string password = "Api!SecurePassword2026";
        Guid userId = Guid.Empty;
        Guid tenantId = Guid.Empty;
        try
        {
            using var unauthorizedProfile = await client.GetAsync("/api/v1/auth/me");
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedProfile.StatusCode);
            using var noAuthLogoutAll = await client.PostAsync("/api/v1/auth/logout-all", JsonContent.Create(new { }));
            Assert.Equal(HttpStatusCode.Unauthorized, noAuthLogoutAll.StatusCode);

            using var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                firstName = "Api", lastName = "Test", email, phoneNumber = (string?)null,
                password, confirmPassword = password, tenantName = "HTTP Integration Wedding", tenantType = 1
            });
            Assert.Equal(HttpStatusCode.Created, register.StatusCode);
            using var registrationJson = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
            userId = registrationJson.RootElement.GetProperty("userId").GetGuid();
            using (var dbScope = factory.Services.CreateScope())
            {
                var db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                tenantId = await db.TenantMemberships.Where(x => x.UserId == userId).Select(x => x.TenantId).SingleAsync();
            }

            using var unverifiedLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            Assert.Equal(HttpStatusCode.Unauthorized, unverifiedLogin.StatusCode);
            using var resendKnown = await client.PostAsJsonAsync("/api/v1/auth/resend-verification", new { email });
            using var resendUnknown = await client.PostAsJsonAsync("/api/v1/auth/resend-verification", new { email = "unknown@example.test" });
            Assert.Equal(HttpStatusCode.OK, resendKnown.StatusCode);
            Assert.Equal(await resendUnknown.Content.ReadAsStringAsync(), await resendKnown.Content.ReadAsStringAsync());
            using var badVerification = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { email, verificationToken = "invalid" });
            Assert.Equal(HttpStatusCode.BadRequest, badVerification.StatusCode);
            var verification = capturedEmail.Messages.Last();
            var verificationToken = ExtractToken(verification.HtmlBody);
            using var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { email, verificationToken });
            Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
            using var duplicate = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                firstName = "Api", lastName = "Test", email, phoneNumber = (string?)null,
                password, confirmPassword = password, tenantName = "Duplicate", tenantType = 1
            });
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

            using var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Wrong!Password2026" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
            using var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
            var login = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
            Assert.NotNull(login);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
            using var me = await client.GetAsync("/api/v1/auth/me");
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            using var profile = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
            Assert.Equal(email.ToUpperInvariant(), profile.RootElement.GetProperty("email").GetString());
            Assert.False(profile.RootElement.TryGetProperty("passwordHash", out _));
            using var tenants = await client.GetAsync("/api/v1/auth/tenants");
            Assert.Equal(HttpStatusCode.OK, tenants.StatusCode);
            using var tenantList = JsonDocument.Parse(await tenants.Content.ReadAsStringAsync());
            Assert.Single(tenantList.RootElement.EnumerateArray());

            using var switched = await client.PostAsJsonAsync("/api/v1/auth/switch-tenant", new { tenantId });
            Assert.Equal(HttpStatusCode.OK, switched.StatusCode);
            using var deniedSwitch = await client.PostAsJsonAsync("/api/v1/auth/switch-tenant", new { tenantId = Guid.NewGuid() });
            Assert.Equal(HttpStatusCode.Forbidden, deniedSwitch.StatusCode);

            using var rotatedResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = login.RefreshToken });
            Assert.Equal(HttpStatusCode.OK, rotatedResponse.StatusCode);
            using var rotatedJson = JsonDocument.Parse(await rotatedResponse.Content.ReadAsStringAsync());
            var replacement = rotatedJson.RootElement.GetProperty("refreshToken").GetString()!;
            using var reused = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = login.RefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
            using var replacementAfterReuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = replacement });
            Assert.Equal(HttpStatusCode.Unauthorized, replacementAfterReuse.StatusCode);

            client.DefaultRequestHeaders.Authorization = null;
            using var logoutLoginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            var logoutLogin = await logoutLoginResponse.Content.ReadFromJsonAsync<TokenResponse>();
            using var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = logoutLogin!.RefreshToken });
            Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
            using var repeatedLogout = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = logoutLogin.RefreshToken });
            Assert.Equal(HttpStatusCode.OK, repeatedLogout.StatusCode);
            using var afterLogout = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = logoutLogin.RefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);

            using var allSessionsLoginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            var allSessionsLogin = await allSessionsLoginResponse.Content.ReadFromJsonAsync<TokenResponse>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", allSessionsLogin!.AccessToken);
            using var logoutAll = await client.PostAsync("/api/v1/auth/logout-all", JsonContent.Create(new { }));
            Assert.Equal(HttpStatusCode.OK, logoutAll.StatusCode);
            client.DefaultRequestHeaders.Authorization = null;
            using var afterLogoutAll = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = allSessionsLogin.RefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, afterLogoutAll.StatusCode);

            using var forgotUnknown = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "unknown@example.test" });
            using var forgotKnown = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
            Assert.Equal(await forgotUnknown.Content.ReadAsStringAsync(), await forgotKnown.Content.ReadAsStringAsync());
            var resetToken = ExtractToken(capturedEmail.Messages.Last().HtmlBody);
            const string newPassword = "Api!BrandNewPassword2027";
            using var invalidReset = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email, resetToken = "invalid", newPassword, confirmPassword = newPassword });
            Assert.Equal(HttpStatusCode.BadRequest, invalidReset.StatusCode);
            using var reset = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email, resetToken, newPassword, confirmPassword = newPassword });
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
            using var oldPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
            using var newPasswordLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = newPassword });
            Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
        }
        finally
        {
            using var cleanupScope = factory.Services.CreateScope();
            var db = cleanupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (userId != Guid.Empty)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from public.tenant_memberships where user_id = {userId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from public.tenants where id = {tenantId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from public.asp_net_users where id = {userId}");
            }
        }
    }

    private static string ExtractToken(string html)
    {
        var match = Regex.Match(html, "token=([^&\\\"]+)");
        Assert.True(match.Success);
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    private sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAtUtc);

    private sealed class CapturingEmailService : IEmailService
    {
        public List<EmailMessage> Messages { get; } = [];
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) { Messages.Add(message); return Task.CompletedTask; }
    }

    private sealed class TemporaryEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> _prior;
        public TemporaryEnvironment(IReadOnlyDictionary<string, string?> values)
        {
            _prior = values.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
            foreach (var (key, value) in values) Environment.SetEnvironmentVariable(key, value);
        }
        public void Dispose()
        {
            foreach (var (key, value) in _prior) Environment.SetEnvironmentVariable(key, value);
        }
    }

    private sealed class AuthenticationApiFactory(string connectionString, IEmailService capturedEmail, string signingKey) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Jwt:Issuer"] = "WeddingSaaS.IntegrationTests", ["Jwt:Audience"] = "WeddingSaaS.IntegrationTests.Client",
                ["Jwt:SigningKey"] = signingKey, ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "30",
                ["Email:FrontendBaseUrl"] = "http://localhost:4200"
            }));
            builder.ConfigureTestServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.RemoveAll<IEmailService>();
                services.AddSingleton(capturedEmail);
                services.AddSingleton<IEmailService>(capturedEmail);
            });
        }
    }
}
