using WeddingSaaS.Domain.Common;
using WeddingSaaS.Domain.Enums;
using WeddingSaaS.Domain.Exceptions;
using WeddingSaaS.Domain.ValueObjects;

namespace WeddingSaaS.Domain.Entities;

public sealed class TenantInvitation : TenantEntity
{
    // EF Core materialization only.
    private TenantInvitation() : base() { Email = null!; InvitationTokenHash = string.Empty; }

    public TenantInvitation(Guid id, Guid tenantId, string email, Guid roleId, string invitationTokenHash, DateTimeOffset expiresAt, Guid invitedBy, DateTimeOffset? createdAt = null)
        : base(id, tenantId, createdAt)
    {
        Email = EmailAddress.Create(email);
        if (roleId == Guid.Empty) throw new ValidationException(nameof(RoleId), "RoleId is required.");
        if (invitationTokenHash is null || invitationTokenHash.Length != 64 || invitationTokenHash.Any(c => !Uri.IsHexDigit(c))) throw new ValidationException(nameof(InvitationTokenHash), "Invitation token hash must be a 64-character SHA-256 hexadecimal digest; plaintext tokens must never be stored.");
        if (invitedBy == Guid.Empty) throw new ValidationException(nameof(InvitedBy), "InvitedBy is required.");
        RoleId = roleId;
        InvitationTokenHash = invitationTokenHash.ToLowerInvariant();
        ExpiresAt = expiresAt.ToUniversalTime();
        InvitedBy = invitedBy;
        Status = TenantInvitationStatus.Pending;
    }

    public EmailAddress Email { get; private set; }
    public Guid RoleId { get; private set; }
    public string InvitationTokenHash { get; private set; }
    public TenantInvitationStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid InvitedBy { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }

    public void Accept(DateTimeOffset? at = null)
    {
        if (Status == TenantInvitationStatus.Accepted) throw new BusinessRuleException("Invitation has already been accepted.");
        if (Status == TenantInvitationStatus.Revoked) throw new BusinessRuleException("A revoked invitation cannot be accepted.");
        if (Status == TenantInvitationStatus.Expired || (at ?? DateTimeOffset.UtcNow).ToUniversalTime() >= ExpiresAt)
        {
            Status = TenantInvitationStatus.Expired;
            throw new BusinessRuleException("An expired invitation cannot be accepted.");
        }
        AcceptedAt = (at ?? DateTimeOffset.UtcNow).ToUniversalTime();
        Status = TenantInvitationStatus.Accepted;
    }

    public void Revoke()
    {
        if (Status == TenantInvitationStatus.Accepted) throw new BusinessRuleException("An accepted invitation cannot be revoked.");
        Status = TenantInvitationStatus.Revoked;
    }
}

