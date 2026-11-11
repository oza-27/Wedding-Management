using WeddingSaaS.Domain.Entities;
using WeddingSaaS.Domain.Enums;
using WeddingSaaS.Domain.Exceptions;
using WeddingSaaS.Domain.ValueObjects;

namespace WeddingSaaS.UnitTests.Domain;

public sealed class TenantDomainTests
{
    [Fact]
    public void Creates_tenant_with_normalized_values_and_application_supplied_id()
    {
        var id = Guid.NewGuid();
        var tenant = new Tenant(id, "  Golden Days  ", " Golden_Days ", TenantType.Planner, "HELLO@example.com", "+91 98765-43210");

        Assert.Equal(id, tenant.Id);
        Assert.Equal("Golden Days", tenant.Name);
        Assert.Equal("golden-days", tenant.Slug.Value);
        Assert.Equal("hello@example.com", tenant.Email!.Value);
        Assert.Equal("+919876543210", tenant.Phone!.Value);
        Assert.True(tenant.IsActive);
    }

    [Fact] public void Rejects_empty_tenant_name() => Assert.Throws<ValidationException>(() => new Tenant(Guid.NewGuid(), "  ", "good-slug", TenantType.Planner));
    [Fact] public void Rejects_invalid_tenant_slug() => Assert.Throws<ValidationException>(() => new Tenant(Guid.NewGuid(), "Name", "bad/slug", TenantType.Planner));
    [Fact] public void Rejects_invalid_tenant_type() => Assert.Throws<ValidationException>(() => new Tenant(Guid.NewGuid(), "Name", "valid", (TenantType)99));
    [Fact] public void Rejects_invalid_optional_email() => Assert.Throws<ValidationException>(() => new Tenant(Guid.NewGuid(), "Name", "valid", TenantType.Family, "not-an-email"));

    [Fact]
    public void Tenant_aggregate_rejects_duplicate_active_memberships_for_same_user()
    {
        var tenant = CreateTenant();
        var userId = Guid.NewGuid();
        tenant.AddMembership(new TenantMembership(Guid.NewGuid(), tenant.Id, userId, Guid.NewGuid(), TenantMembershipStatus.Active));

        var exception = Assert.Throws<BusinessRuleException>(() => tenant.AddMembership(new TenantMembership(Guid.NewGuid(), tenant.Id, userId, Guid.NewGuid(), TenantMembershipStatus.Active)));
        Assert.Contains("duplicate active", exception.Message);
    }

    [Fact]
    public void Membership_requires_ids_and_only_active_members_can_operate()
    {
        var tenantId = Guid.NewGuid();
        var membership = new TenantMembership(Guid.NewGuid(), tenantId, Guid.NewGuid(), Guid.NewGuid(), TenantMembershipStatus.Active);
        membership.EnsureCanPerformTenantOperations();
        membership.Suspend();
        Assert.Throws<BusinessRuleException>(membership.EnsureCanPerformTenantOperations);
        Assert.Throws<ValidationException>(() => new TenantMembership(Guid.NewGuid(), tenantId, Guid.Empty, Guid.NewGuid()));
        Assert.Throws<ValidationException>(() => new TenantMembership(Guid.NewGuid(), tenantId, Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void Invitation_requires_valid_email_and_hash()
    {
        Assert.Throws<ValidationException>(() => CreateInvitation(email: "bad-email"));
        Assert.Throws<ValidationException>(() => CreateInvitation(tokenHash: "plain-token"));
    }

    [Fact]
    public void Expired_invitation_cannot_be_accepted()
    {
        var invitation = CreateInvitation(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Throws<BusinessRuleException>(() => invitation.Accept(DateTimeOffset.UtcNow));
        Assert.Equal(TenantInvitationStatus.Expired, invitation.Status);
        Assert.Null(invitation.AcceptedAt);
    }

    [Fact]
    public void Revoked_and_accepted_invitations_cannot_be_accepted_again()
    {
        var revoked = CreateInvitation();
        revoked.Revoke();
        Assert.Throws<BusinessRuleException>(() => revoked.Accept());

        var accepted = CreateInvitation();
        accepted.Accept(DateTimeOffset.UtcNow);
        Assert.Throws<BusinessRuleException>(() => accepted.Accept());
        Assert.NotNull(accepted.AcceptedAt);
    }

    private static Tenant CreateTenant() => new(Guid.NewGuid(), "Wedding Studio", "wedding-studio", TenantType.Planner);
    private static TenantInvitation CreateInvitation(string email = "guest@example.com", string tokenHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", DateTimeOffset? expiresAt = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), email, Guid.NewGuid(), tokenHash, expiresAt ?? DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid());
}


