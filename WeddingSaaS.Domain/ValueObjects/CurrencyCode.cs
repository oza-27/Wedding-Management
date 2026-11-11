using WeddingSaaS.Domain.Exceptions;

namespace WeddingSaaS.Domain.ValueObjects;

public sealed record CurrencyCode
{
    private CurrencyCode(string value) => Value = value;
    public string Value { get; }

    public static CurrencyCode Create(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (normalized is null || normalized.Length != 3 || normalized.Any(c => c is < 'A' or > 'Z'))
            throw new ValidationException(nameof(CurrencyCode), "Currency code must be a three-letter ISO-style code.");
        return new CurrencyCode(normalized);
    }
    public override string ToString() => Value;
}
