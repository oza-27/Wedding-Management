using WeddingSaaS.Domain.Common;
using WeddingSaaS.Domain.Enums;
using WeddingSaaS.Domain.Exceptions;
using WeddingSaaS.Domain.ValueObjects;

namespace WeddingSaaS.Domain.Entities;

public sealed class Tenant : BaseEntity
{
    private readonly List<TenantMembership> _memberships = [];

    // EF Core materialization only; validation belongs to the public creation path below.
    private Tenant() : base()
    {
        Name = string.Empty;
        Slug = null!;
    }

    public Tenant(Guid id, string name, string slug, TenantType tenantType, string? email = null, string? phone = null, DateTimeOffset? createdAt = null)
        : base(id, createdAt)
    {
        Name = Required(name, nameof(Name));
        Slug = TenantSlug.Create(slug);
        if (!Enum.IsDefined(tenantType)) throw new ValidationException(nameof(TenantType), "Tenant type is invalid.");
        TenantType = tenantType;
        Email = string.IsNullOrWhiteSpace(email) ? null : EmailAddress.Create(email);
        Phone = string.IsNullOrWhiteSpace(phone) ? null : PhoneNumber.Create(phone);
        IsActive = true;
    }

    public string Name { get; private set; }
    public TenantSlug Slug { get; private set; }
    public TenantType TenantType { get; private set; }
    public EmailAddress? Email { get; private set; }
    public PhoneNumber? Phone { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<TenantMembership> Memberships => _memberships.AsReadOnly();

    public void AddMembership(TenantMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        if (membership.TenantId != Id) throw new BusinessRuleException("Membership must belong to this tenant.");
        if (membership.MembershipStatus == TenantMembershipStatus.Active && _memberships.Any(m => m.UserId == membership.UserId && m.MembershipStatus == TenantMembershipStatus.Active))
            throw new BusinessRuleException("A user cannot have duplicate active memberships in the same tenant.");
        if (_memberships.Any(m => m.Id == membership.Id)) throw new BusinessRuleException("Membership is already attached to this tenant.");
        _memberships.Add(membership);
    }

    public void ActivateMembership(Guid membershipId, DateTimeOffset? joinedAt = null)
    {
        var membership = _memberships.SingleOrDefault(m => m.Id == membershipId)
            ?? throw new BusinessRuleException("Membership is not attached to this tenant.");
        if (_memberships.Any(m => m.Id != membershipId && m.UserId == membership.UserId && m.MembershipStatus == TenantMembershipStatus.Active))
            throw new BusinessRuleException("A user cannot have duplicate active memberships in the same tenant.");
        membership.Activate(joinedAt);
    }

    public void UpdateProfile(string name, string slug, string? email, string? phone, Guid? updatedBy = null, DateTimeOffset? at = null)
    {
        Name = Required(name, nameof(Name));
        Slug = TenantSlug.Create(slug);
        Email = string.IsNullOrWhiteSpace(email) ? null : EmailAddress.Create(email);
        Phone = string.IsNullOrWhiteSpace(phone) ? null : PhoneNumber.Create(phone);
        RecordUpdate(updatedBy, at);
    }

    public void Deactivate(Guid? updatedBy = null, DateTimeOffset? at = null)
    {
        IsActive = false;
        RecordUpdate(updatedBy, at);
    }

    public void Activate(Guid? updatedBy = null, DateTimeOffset? at = null)
    {
        IsActive = true;
        RecordUpdate(updatedBy, at);
    }

    private static string Required(string value, string property)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ValidationException(property, $"{property} is required.");
        return normalized;
    }
}
