using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WeddingSaaS.Application.Abstractions.Authentication;

namespace WeddingSaaS.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthenticationService authentication) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting("auth-registration")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await authentication.RegisterAsync(new(request.FirstName, request.LastName, request.Email, request.PhoneNumber, request.Password, request.ConfirmPassword, request.TenantName, request.TenantType), ct);
            var message = result.VerificationEmailSent ? "Registration created. Check your email to verify your account." : "Registration created, but verification email delivery failed. Request a new verification email after email delivery is configured.";
            return StatusCode(StatusCodes.Status201Created, new RegisterResponse(result.UserId, message));
        }
        catch (DuplicateEmailException) { return Conflict(new { message = "Unable to register with the supplied details." }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (IdentityOperationException) { return BadRequest(new { message = "Registration requirements were not met." }); }
    }

    [HttpPost("verify-email")]
    [EnableRateLimiting("auth-verification")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailDto request, CancellationToken ct) =>
        await authentication.VerifyEmailAsync(new(request.Email, request.VerificationToken), ct) ? Ok(new { message = "Email verified." }) : BadRequest(new { message = "Verification link is invalid, expired, or already used." });

    [HttpPost("resend-verification")]
    [EnableRateLimiting("auth-verification")]
    public async Task<IActionResult> ResendVerification(EmailDto request, CancellationToken ct)
    {
        await authentication.ResendVerificationAsync(request.Email, ct);
        return Ok(new { message = "If the account needs verification, an email will be sent." });
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth-login")]
    public async Task<IActionResult> Login(LoginDto request, CancellationToken ct)
    {
        var pair = await authentication.LoginAsync(new(request.Email, request.Password), HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return pair is null ? Unauthorized(new { message = "Email or password is incorrect, or the account is not eligible to sign in." }) : Ok(pair);
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth-refresh")]
    public async Task<IActionResult> Refresh(RefreshDto request, CancellationToken ct)
    {
        var pair = await authentication.RefreshAsync(request.RefreshToken, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return pair is null ? Unauthorized(new { message = "Refresh token is invalid or expired." }) : Ok(pair);
    }

    [HttpPost("logout")]
    [EnableRateLimiting("auth-write")]
    public async Task<IActionResult> Logout(RefreshDto request, CancellationToken ct)
    {
        await authentication.LogoutAsync(request.RefreshToken, ct);
        return Ok(new { message = "Session signed out." });
    }

    [Authorize, HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await authentication.LogoutAllAsync(userId, ct);
        return Ok(new { message = "All sessions signed out." });
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth-recovery")]
    public async Task<IActionResult> ForgotPassword(EmailDto request, CancellationToken ct)
    {
        await authentication.ForgotPasswordAsync(request.Email, ct);
        return Ok(new { message = "If the account is eligible, password reset instructions will be sent." });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth-recovery")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto request, CancellationToken ct) =>
        await authentication.ResetPasswordAsync(new(request.Email, request.ResetToken, request.NewPassword, request.ConfirmPassword), ct) ? Ok(new { message = "Password reset." }) : BadRequest(new { message = "Password reset link is invalid, expired, or the new password does not meet requirements." });

    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var profile = await authentication.GetProfileAsync(userId, ct);
        return profile is null ? Unauthorized() : Ok(profile);
    }

    [Authorize, HttpGet("tenants")]
    public async Task<IActionResult> Tenants(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await authentication.GetTenantsAsync(userId, ct));
    }

    [Authorize, HttpPost("switch-tenant")]
    [EnableRateLimiting("auth-write")]
    public async Task<IActionResult> SwitchTenant(SwitchTenantDto request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var pair = await authentication.SwitchTenantAsync(userId, request.TenantId, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return pair is null ? Forbid() : Ok(pair);
    }

    private bool TryGetUserId(out Guid id) => Guid.TryParse(User.FindFirstValue("sub"), out id);
}

public sealed record RegisterRequestDto([Required, StringLength(100, MinimumLength = 1)] string FirstName, [Required, StringLength(100, MinimumLength = 1)] string LastName,
    [Required, EmailAddress, StringLength(254)] string Email, [Phone, StringLength(16)] string? PhoneNumber,
    [Required, StringLength(128, MinimumLength = 12)] string Password, [Required] string ConfirmPassword,
    [Required, StringLength(200, MinimumLength = 1)] string TenantName, WeddingSaaS.Domain.Enums.TenantType TenantType);
public sealed record VerifyEmailDto([Required, EmailAddress] string Email, [Required] string VerificationToken);
public sealed record EmailDto([Required, EmailAddress] string Email);
public sealed record LoginDto([Required, EmailAddress] string Email, [Required] string Password);
public sealed record RefreshDto([Required] string RefreshToken);
public sealed record ResetPasswordDto([Required, EmailAddress] string Email, [Required] string ResetToken, [Required] string NewPassword, [Required] string ConfirmPassword);
public sealed record SwitchTenantDto([Required] Guid TenantId);
public sealed record RegisterResponse(Guid UserId, string Message);
