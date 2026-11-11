using WeddingSaaS.Domain.Common;
using WeddingSaaS.Domain.Enums;
using WeddingSaaS.Domain.Exceptions;

namespace WeddingSaaS.Domain.Entities;

public sealed class TenantMembership : TenantEntity
{
    // EF Core materialization only.
    private TenantMembership() : base() { }

    public TenantMembership(Guid id, Guid tenantId, Guid userId, Guid roleId, TenantMembershipStatus status = TenantMembershipStatus.Pending, DateTimeOffset? joinedAt = null, DateTimeOffset? createdAt = null)
        : base(id, tenantId, createdAt)
    {
        if (userId == Guid.Empty) throw new ValidationException(nameof(UserId), "UserId is required.");
        if (roleId == Guid.Empty) throw new ValidationException(nameof(RoleId), "RoleId is required.");
        if (!Enum.IsDefined(status)) throw new ValidationException(nameof(MembershipStatus), "Membership status is invalid.");
        UserId = userId;
        RoleId = roleId;
        MembershipStatus = status;
        JoinedAt = status == TenantMembershipStatus.Active ? (joinedAt ?? DateTimeOffset.UtcNow).ToUniversalTime() : joinedAt?.ToUniversalTime();
    }

    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public TenantMembershipStatus MembershipStatus { get; private set; }
    public DateTimeOffset? JoinedAt { get; private set; }

    public void EnsureCanPerformTenantOperations()
    {
        if (MembershipStatus != TenantMembershipStatus.Active)
            throw new BusinessRuleException($"A {MembershipStatus.ToString().ToLowerInvariant()} membership cannot perform tenant operations.");
    }

    internal void Activate(DateTimeOffset? joinedAt = null)
    {
        if (MembershipStatus is TenantMembershipStatus.Revoked)
            throw new BusinessRuleException("A revoked membership cannot be reactivated.");
        MembershipStatus = TenantMembershipStatus.Active;
        JoinedAt = (joinedAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }

    public void Suspend()
    {
        if (MembershipStatus is TenantMembershipStatus.Revoked) throw new BusinessRuleException("A revoked membership cannot be suspended.");
        MembershipStatus = TenantMembershipStatus.Suspended;
    }

    public void Revoke() => MembershipStatus = TenantMembershipStatus.Revoked;
}
