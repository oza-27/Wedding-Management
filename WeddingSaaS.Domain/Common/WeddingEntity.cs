namespace WeddingSaaS.Domain.Common;

public abstract class WeddingEntity : TenantEntity
{
    // EF Core materialization only.
    protected WeddingEntity() : base() { }

    protected WeddingEntity(Guid id, Guid tenantId, Guid weddingId, DateTimeOffset? createdAt = null) : base(id, tenantId, createdAt)
    {
        if (weddingId == Guid.Empty) throw new Exceptions.ValidationException(nameof(WeddingId), "WeddingId is required.");
        WeddingId = weddingId;
    }

    public Guid WeddingId { get; private set; }
}
