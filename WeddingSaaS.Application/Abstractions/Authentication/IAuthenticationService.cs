using WeddingSaaS.Domain.Enums;

namespace WeddingSaaS.Application.Abstractions.Authentication;

public interface IAuthenticationService
{
    Task<RegistrationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<bool> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken);
    Task ResendVerificationAsync(string email, CancellationToken cancellationToken);
    Task<TokenPair?> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken);
    Task<TokenPair?> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken);
    Task ForgotPasswordAsync(string email, CancellationToken cancellationToken);
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
    Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TenantMembershipInfo>> GetTenantsAsync(Guid userId, CancellationToken cancellationToken);
    Task<TokenPair?> SwitchTenantAsync(Guid userId, Guid tenantId, string? ipAddress, CancellationToken cancellationToken);
}

public sealed record RegisterRequest(string FirstName, string LastName, string Email, string? PhoneNumber, string Password, string ConfirmPassword, string TenantName, TenantType TenantType);
public sealed record VerifyEmailRequest(string Email, string VerificationToken);
public sealed record LoginRequest(string Email, string Password);
public sealed record ResetPasswordRequest(string Email, string ResetToken, string NewPassword, string ConfirmPassword);
public sealed record RegistrationResult(Guid UserId, bool VerificationEmailSent);
public sealed record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAtUtc);
public sealed record UserProfile(Guid UserId, string FirstName, string LastName, string Email, bool IsEmailVerified, string? PhoneNumber, bool IsActive);
public sealed record TenantMembershipInfo(Guid TenantId, string TenantName, string TenantSlug, TenantType TenantType, string MembershipRole, string MembershipStatus);
public sealed class DuplicateEmailException : Exception { }
public sealed class IdentityOperationException(IReadOnlyList<string> errorCodes) : Exception("Identity operation failed.") { public IReadOnlyList<string> ErrorCodes { get; } = errorCodes; }
