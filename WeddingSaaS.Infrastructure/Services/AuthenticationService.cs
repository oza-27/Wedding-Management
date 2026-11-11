using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using WeddingSaaS.Application.Abstractions.Authentication;
using WeddingSaaS.Application.Abstractions.Email;
using WeddingSaaS.Application.Common;
using WeddingSaaS.Domain.Entities;
using WeddingSaaS.Domain.Enums;
using WeddingSaaS.Domain.ValueObjects;
using WeddingSaaS.Infrastructure.Identity;
using WeddingSaaS.Infrastructure.Persistence.Context;
using WeddingSaaS.Infrastructure.Security;

namespace WeddingSaaS.Infrastructure.Services;

public sealed class AuthenticationService(UserManager<ApplicationUser> users, ApplicationDbContext db,
    ITokenService tokens, IEmailService email, IConfiguration configuration, IHostEnvironment environment, ILogger<AuthenticationService> logger) : IAuthenticationService
{
    private static readonly Guid TenantAdminRoleId = Guid.Parse("10100000-0000-0000-0000-000000000001");

    public async Task<RegistrationResult> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var errors = AuthenticationRequestValidator.Validate(request);
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
        var address = NormalizeEmail(request.Email);
        _ = EmailAddress.Create(address);
        if (await users.FindByEmailAsync(address) is not null) throw new DuplicateEmailException();

        ApplicationUser user;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            user = new ApplicationUser { Id = Guid.NewGuid(), UserName = address, Email = address, PhoneNumber = request.PhoneNumber, FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                if (result.Errors.Any(x => x.Code.Contains("DuplicateUserName", StringComparison.OrdinalIgnoreCase))) throw new DuplicateEmailException();
                throw new IdentityOperationException(result.Errors.Select(x => x.Code).ToArray());
            }
            var slugBase = Regex.Replace(request.TenantName.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (string.IsNullOrEmpty(slugBase)) slugBase = "workspace";
            if (slugBase.Length > 53) slugBase = slugBase[..53].TrimEnd('-');
            var tenant = new Tenant(Guid.NewGuid(), request.TenantName, $"{slugBase}-{user.Id:N}"[..Math.Min(slugBase.Length + 9, 63)], request.TenantType, address, request.PhoneNumber);
            db.Tenants.Add(tenant);
            db.TenantMemberships.Add(new TenantMembership(Guid.NewGuid(), tenant.Id, user.Id, TenantAdminRoleId, TenantMembershipStatus.Active));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_asp_net_users_normalized_email" })
        {
            throw new DuplicateEmailException();
        }

        // SMTP cannot participate in the database transaction. A delivery failure leaves a complete, unverified account that can safely request a resend.
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var sent = await TrySendLinkAsync(address, "verify-email", token, "Verify your Wedding SaaS email", ct);
        return new RegistrationResult(user.Id, sent);
    }

    public async Task<bool> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(NormalizeEmail(request.Email));
        if (user is null || !user.IsActive || user.EmailConfirmed) return false;
        return (await users.ConfirmEmailAsync(user, request.VerificationToken)).Succeeded;
    }

    public async Task ResendVerificationAsync(string address, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(NormalizeEmail(address));
        if (user is null || !user.IsActive || user.EmailConfirmed) return;
        await TrySendLinkAsync(user.Email!, "verify-email", await users.GenerateEmailConfirmationTokenAsync(user), "Verify your Wedding SaaS email", ct);
    }

    public async Task<TokenPair?> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(NormalizeEmail(request.Email));
        if (user is null || !user.IsActive || !user.EmailConfirmed || await users.IsLockedOutAsync(user)) return null;
        if (!await users.CheckPasswordAsync(user, request.Password)) { await users.AccessFailedAsync(user); return null; }
        await users.ResetAccessFailedCountAsync(user);
        return await IssueAsync(user, null, ipAddress, ct);
    }

    public async Task<TokenPair?> RefreshAsync(string raw, string? ipAddress, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var hash = tokens.HashRefreshToken(raw);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var old = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (old is null) return null;
        if (old.RevokedAtUtc.HasValue)
        {
            if (old.ReplacedByTokenHash is not null) await RevokeAllAsync(old.UserId, "refresh_token_reuse", ct);
            await tx.CommitAsync(ct);
            return null;
        }
        var now = DateTimeOffset.UtcNow;
        if (old.ExpiresAtUtc <= now) { old.Revoke(now, "expired"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return null; }
        var user = await users.FindByIdAsync(old.UserId.ToString());
        if (!await IsEligibleAsync(user)) return null;
        if (old.TenantId.HasValue && !await HasMembershipAsync(user!.Id, old.TenantId.Value, ct)) { await RevokeOneAsync(old.Id, "membership_ineligible", ct); await tx.CommitAsync(ct); return null; }

        var replacement = tokens.CreateRefreshToken();
        var replacementHash = tokens.HashRefreshToken(replacement);
        var changed = await db.RefreshTokens.Where(x => x.Id == old.Id && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now).SetProperty(x => x.ReplacedByTokenHash, replacementHash).SetProperty(x => x.RevocationReason, "rotated"), ct);
        if (changed != 1) { await RevokeAllAsync(old.UserId, "refresh_token_reuse", ct); await tx.CommitAsync(ct); return null; }
        db.RefreshTokens.Add(NewRefresh(user!.Id, replacementHash, ipAddress, old.TenantId, now));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        var access = tokens.CreateAccessToken(user, old.TenantId);
        return new TokenPair(access.Token, replacement, access.ExpiresAtUtc);
    }

    public async Task LogoutAsync(string raw, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        var token = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == tokens.HashRefreshToken(raw), ct);
        if (token is { RevokedAtUtc: null }) { token.Revoke(DateTimeOffset.UtcNow, "logout"); await db.SaveChangesAsync(ct); }
    }

    public async Task LogoutAllAsync(Guid userId, CancellationToken ct) => await RevokeAllAsync(userId, "logout_all", ct);

    public async Task ForgotPasswordAsync(string address, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(NormalizeEmail(address));
        if (user is null || !user.IsActive || !user.EmailConfirmed) return;
        await TrySendLinkAsync(user.Email!, "reset-password", await users.GeneratePasswordResetTokenAsync(user), "Reset your Wedding SaaS password", ct);
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        if (request.NewPassword != request.ConfirmPassword) return false;
        var user = await users.FindByEmailAsync(NormalizeEmail(request.Email));
        if (user is null || !user.IsActive || !user.EmailConfirmed) return false;
        var result = await users.ResetPasswordAsync(user, request.ResetToken, request.NewPassword);
        if (!result.Succeeded) return false;
        await RevokeAllAsync(user.Id, "password_reset", ct);
        return true;
    }

    public async Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is null ? null : new UserProfile(user.Id, user.FirstName, user.LastName, user.Email ?? "", user.EmailConfirmed, user.PhoneNumber, user.IsActive);
    }

    public async Task<IReadOnlyList<TenantMembershipInfo>> GetTenantsAsync(Guid userId, CancellationToken ct) =>
        await (from m in db.TenantMemberships.AsNoTracking()
               join t in db.Tenants.AsNoTracking() on m.TenantId equals t.Id
               join r in db.Roles.AsNoTracking() on m.RoleId equals r.Id
               where m.UserId == userId && m.MembershipStatus == TenantMembershipStatus.Active && t.IsActive
               select new TenantMembershipInfo(t.Id, t.Name, t.Slug.Value, t.TenantType, r.Name ?? "", m.MembershipStatus.ToString())).ToListAsync(ct);

    public async Task<TokenPair?> SwitchTenantAsync(Guid userId, Guid tenantId, string? ipAddress, CancellationToken ct)
    {
        if (!await HasMembershipAsync(userId, tenantId, ct)) return null;
        var user = await users.FindByIdAsync(userId.ToString());
        if (!await IsEligibleAsync(user)) return null;
        return await IssueAsync(user!, tenantId, ipAddress, ct);
    }

    private async Task<TokenPair> IssueAsync(ApplicationUser user, Guid? tenantId, string? ip, CancellationToken ct)
    {
        var access = tokens.CreateAccessToken(user, tenantId);
        var raw = tokens.CreateRefreshToken();
        var now = DateTimeOffset.UtcNow;
        db.RefreshTokens.Add(NewRefresh(user.Id, tokens.HashRefreshToken(raw), ip, tenantId, now));
        await db.SaveChangesAsync(ct);
        return new TokenPair(access.Token, raw, access.ExpiresAtUtc);
    }

    private RefreshToken NewRefresh(Guid userId, string hash, string? ip, Guid? tenantId, DateTimeOffset now) => new(Guid.NewGuid(), userId, hash, now, now.AddDays(RefreshDays), ip, tenantId);
    private int RefreshDays => int.TryParse(configuration["Jwt:RefreshTokenDays"], out var days) ? days : 30;
    private async Task<bool> IsEligibleAsync(ApplicationUser? user) => user is { IsActive: true, EmailConfirmed: true } && !await users.IsLockedOutAsync(user);
    private Task<bool> HasMembershipAsync(Guid userId, Guid tenantId, CancellationToken ct) => db.TenantMemberships.AnyAsync(m => m.UserId == userId && m.TenantId == tenantId && m.MembershipStatus == TenantMembershipStatus.Active && db.Tenants.Any(t => t.Id == tenantId && t.IsActive), ct);
    private async Task RevokeAllAsync(Guid userId, string reason, CancellationToken ct) => await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTimeOffset.UtcNow).SetProperty(x => x.RevocationReason, reason), ct);
    private async Task RevokeOneAsync(Guid id, string reason, CancellationToken ct) => await db.RefreshTokens.Where(x => x.Id == id && x.RevokedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTimeOffset.UtcNow).SetProperty(x => x.RevocationReason, reason), ct);

    private async Task SendLinkAsync(string address, string path, string rawToken, string subject, CancellationToken ct)
    {
        var baseUrl = configuration["Email:FrontendBaseUrl"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || (!environment.IsDevelopment() && uri.Scheme != Uri.UriSchemeHttps)) throw new InvalidOperationException("Email:FrontendBaseUrl must be an absolute HTTPS URL outside Development.");
        var link = $"{baseUrl!.TrimEnd('/')}/{path}?email={Uri.EscapeDataString(address)}&token={Uri.EscapeDataString(rawToken)}";
        var html = $"<p>Use this secure link to continue:</p><p><a href=\"{HtmlEncoder.Default.Encode(link)}\">Continue</a></p>";
        await email.SendAsync(new EmailMessage(address, subject, html), ct);
    }

    private async Task<bool> TrySendLinkAsync(string address, string path, string token, string subject, CancellationToken ct)
    {
        try { await SendLinkAsync(address, path, token, subject, ct); return true; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning("Authentication email delivery failed. ExceptionType={ExceptionType}", exception.GetType().Name);
            return false;
        }
    }

    private static string NormalizeEmail(string email) => (email ?? "").Trim().ToUpperInvariant();
}
