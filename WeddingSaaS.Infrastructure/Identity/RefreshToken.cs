namespace WeddingSaaS.Infrastructure.Identity;

public sealed class RefreshToken
{
    private RefreshToken() { }

    public RefreshToken(Guid id, Guid userId, string tokenHash, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc, string? createdByIp, Guid? tenantId = null)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        ExpiresAtUtc = expiresAtUtc.ToUniversalTime();
        CreatedByIp = createdByIp;
        TenantId = tenantId;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string? CreatedByIp { get; private set; }
    public string? RevocationReason { get; private set; }
    public Guid? TenantId { get; private set; }

    public void Revoke(DateTimeOffset atUtc, string reason, string? replacementHash = null)
    {
        if (RevokedAtUtc is not null) return;
        RevokedAtUtc = atUtc.ToUniversalTime();
        RevocationReason = reason;
        ReplacedByTokenHash = replacementHash;
    }
}
