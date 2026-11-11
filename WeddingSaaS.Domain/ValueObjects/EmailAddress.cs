using System.Net.Mail;
using WeddingSaaS.Domain.Exceptions;

namespace WeddingSaaS.Domain.ValueObjects;

public sealed record EmailAddress
{
    private EmailAddress(string value) => Value = value;
    public string Value { get; }

    public static EmailAddress Create(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 254 || !MailAddress.TryCreate(normalized, out var parsed) || !string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase) || !normalized.Contains('@'))
            throw new ValidationException(nameof(EmailAddress), "A valid email address is required.");
        return new EmailAddress(normalized);
    }
    public override string ToString() => Value;
}
