using WeddingSaaS.Domain.Exceptions;

namespace WeddingSaaS.Domain.ValueObjects;

/// <summary>International phone number normalized to E.164 (a plus sign followed by 7–15 digits).</summary>
public sealed record PhoneNumber
{
    private PhoneNumber(string value) => Value = value;
    public string Value { get; }

    public static PhoneNumber Create(string value)
    {
        var input = value?.Trim() ?? string.Empty;
        var digits = new string(input.Where(char.IsDigit).ToArray());
        var allowed = input.All(c => char.IsDigit(c) || c is '+' or ' ' or '-' or '(' or ')' or '.');
        if (!allowed || !input.StartsWith('+') || input.Count(c => c == '+') != 1 || digits.Length is < 7 or > 15 || digits[0] == '0')
            throw new ValidationException(nameof(PhoneNumber), "Phone number must be a valid international E.164 number.");
        return new PhoneNumber($"+{digits}");
    }
    public override string ToString() => Value;
}

