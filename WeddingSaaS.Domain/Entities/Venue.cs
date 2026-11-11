using WeddingSaaS.Domain.Common;
using WeddingSaaS.Domain.Exceptions;
using WeddingSaaS.Domain.ValueObjects;

namespace WeddingSaaS.Domain.Entities;

public sealed class Venue : WeddingEntity
{
    // EF Core materialization only.
    private Venue() : base() { Name = string.Empty; }

    public Venue(Guid id, Wedding wedding, string name, string? address = null, decimal? latitude = null, decimal? longitude = null, string? contactPerson = null, string? contactNumber = null, DateTimeOffset? createdAt = null)
        : base(id, wedding?.TenantId ?? Guid.Empty, wedding?.Id ?? Guid.Empty, createdAt)
    {
        Name = Required(name);
        if (latitude.HasValue != longitude.HasValue) throw new ValidationException(nameof(Latitude), "Latitude and longitude must be provided together.");
        if (latitude is < -90 or > 90) throw new ValidationException(nameof(Latitude), "Latitude must be between -90 and 90.");
        if (longitude is < -180 or > 180) throw new ValidationException(nameof(Longitude), "Longitude must be between -180 and 180.");
        Address = NormalizeOptional(address);
        Latitude = latitude;
        Longitude = longitude;
        ContactPerson = NormalizeOptional(contactPerson);
        ContactNumber = string.IsNullOrWhiteSpace(contactNumber) ? null : PhoneNumber.Create(contactNumber);
    }

    public string Name { get; private set; }
    public string? Address { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public string? ContactPerson { get; private set; }
    public PhoneNumber? ContactNumber { get; private set; }

    private static string Required(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ValidationException(nameof(Name), "Venue name is required.");
        return normalized;
    }
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
