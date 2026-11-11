using WeddingSaaS.Domain.Common;
using WeddingSaaS.Domain.Exceptions;
using WeddingSaaS.Domain.ValueObjects;

namespace WeddingSaaS.Domain.Entities;

public sealed class WeddingSettings : WeddingEntity
{
    // EF Core materialization only.
    private WeddingSettings() : base()
    {
        TimeZoneId = string.Empty;
        CurrencyCode = null!;
    }

    public WeddingSettings(Guid id, Wedding wedding, string timeZoneId, string currencyCode = "INR", bool guestPortalEnabled = false, DateTimeOffset? createdAt = null)
        : base(id, wedding?.TenantId ?? Guid.Empty, wedding?.Id ?? Guid.Empty, createdAt)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) throw new ValidationException(nameof(TimeZoneId), "TimeZoneId must be explicitly provided by configuration.");
        try { TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim()); }
        catch (TimeZoneNotFoundException) { throw new ValidationException(nameof(TimeZoneId), $"Unknown time zone '{timeZoneId}'."); }
        catch (InvalidTimeZoneException) { throw new ValidationException(nameof(TimeZoneId), $"Invalid time zone '{timeZoneId}'."); }
        TimeZoneId = timeZoneId.Trim();
        CurrencyCode = CurrencyCode.Create(currencyCode);
        GuestPortalEnabled = guestPortalEnabled;
    }

    public string TimeZoneId { get; private set; }
    public CurrencyCode CurrencyCode { get; private set; }
    public bool GuestPortalEnabled { get; private set; }
}
