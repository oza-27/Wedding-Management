using System.Text.RegularExpressions;
using WeddingSaaS.Domain.Exceptions;

namespace WeddingSaaS.Domain.ValueObjects;

public sealed record TenantSlug
{
    private static readonly Regex ValidPattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private TenantSlug(string value) => Value = value;
    public string Value { get; }

    public static TenantSlug Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException(nameof(TenantSlug), "Tenant slug is required.");
        var normalized = Regex.Replace(value.Trim().ToLowerInvariant().Replace('_', '-'), @"\s+", "-");
        normalized = Regex.Replace(normalized, "-+", "-");
        if (normalized.Length > 63 || !ValidPattern.IsMatch(normalized))
            throw new ValidationException(nameof(TenantSlug), "Tenant slug must contain only lowercase letters, numbers, and single hyphen separators.");
        return new TenantSlug(normalized);
    }
    public override string ToString() => Value;
}
