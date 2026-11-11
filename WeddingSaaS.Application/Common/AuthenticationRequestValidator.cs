using System.ComponentModel.DataAnnotations;
using WeddingSaaS.Application.Abstractions.Authentication;
using WeddingSaaS.Domain.ValueObjects;

namespace WeddingSaaS.Application.Common;

public static class AuthenticationRequestValidator
{
    public static IReadOnlyList<string> Validate(RegisterRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Trim().Length > 100) errors.Add("First name is required and must be at most 100 characters.");
        if (string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Trim().Length > 100) errors.Add("Last name is required and must be at most 100 characters.");
        if (string.IsNullOrWhiteSpace(request.TenantName) || request.TenantName.Trim().Length > 200) errors.Add("Tenant name is required and must be at most 200 characters.");
        if (request.Email?.Trim().Length > 254 || !new EmailAddressAttribute().IsValid(request.Email?.Trim())) errors.Add("A valid email address is required.");
        if (!Enum.IsDefined(request.TenantType)) errors.Add("Tenant type is invalid.");
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal)) errors.Add("Password confirmation does not match.");
        if (request.PhoneNumber is { Length: > 16 }) errors.Add("Phone number must be at most 16 characters.");
        else if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            try { _ = PhoneNumber.Create(request.PhoneNumber); }
            catch (WeddingSaaS.Domain.Exceptions.ValidationException) { errors.Add("Phone number must be a valid international number."); }
        }
        return errors;
    }
}
