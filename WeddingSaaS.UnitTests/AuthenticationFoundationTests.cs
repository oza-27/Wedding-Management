using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using WeddingSaaS.Application.Abstractions.Authentication;
using WeddingSaaS.Application.Common;
using WeddingSaaS.Domain.Enums;
using WeddingSaaS.Infrastructure.Identity;
using WeddingSaaS.Infrastructure.Security;

namespace WeddingSaaS.UnitTests;

public sealed class AuthenticationFoundationTests
{
    [Fact]
    public void Registration_validation_rejects_mismatched_password_and_invalid_email()
    {
        var request = new RegisterRequest("A", "B", "not-an-email", null, "password", "different", "Wedding", TenantType.Family);
        var errors = AuthenticationRequestValidator.Validate(request);
        Assert.Contains(errors, x => x.Contains("email", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, x => x.Contains("confirmation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Registration_validation_accepts_a_valid_request()
    {
        var request = new RegisterRequest("A", "B", "a@example.test", null, "StrongPassword!123", "StrongPassword!123", "Wedding", TenantType.Family);
        Assert.Empty(AuthenticationRequestValidator.Validate(request));
    }

    [Fact]
    public void Access_token_contains_only_identity_and_optional_tenant_context_claims()
    {
        var options = new JwtOptions { Issuer = "test-issuer", Audience = "test-audience", SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)), AccessTokenMinutes = 12, RefreshTokenDays = 20 };
        var service = new TokenService(Options.Create(options));
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "safe@example.test", FirstName = "Ada", LastName = "Rao" };
        var tenantId = Guid.NewGuid();
        var (token, expiry) = service.CreateAccessToken(user, tenantId);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("test-issuer", jwt.Issuer);
        Assert.Contains("test-audience", jwt.Audiences);
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal(tenantId.ToString(), jwt.Claims.Single(c => c.Type == "tenant_id").Value);
        Assert.InRange((expiry.UtcDateTime - jwt.ValidTo).Duration(), TimeSpan.Zero, TimeSpan.FromSeconds(1));
        Assert.DoesNotContain(jwt.Claims, c => c.Type.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Refresh_token_is_random_and_hashing_is_one_way_digest()
    {
        var service = new TokenService(Options.Create(new JwtOptions { Issuer = "i", Audience = "a", SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)) }));
        var raw = service.CreateRefreshToken();
        var hash = service.HashRefreshToken(raw);
        Assert.True(raw.Length >= 80);
        Assert.Equal(64, hash.Length);
        Assert.NotEqual(raw, hash);
        Assert.Equal(hash, service.HashRefreshToken(raw));
        Assert.NotEqual(hash, service.HashRefreshToken(service.CreateRefreshToken()));
    }
}
