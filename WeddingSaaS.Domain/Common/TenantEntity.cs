namespace WeddingSaaS.Domain.Common;

public abstract class TenantEntity : BaseEntity
{
    // EF Core materialization only.
    protected TenantEntity() : base() { }

    protected TenantEntity(Guid id, Guid tenantId, DateTimeOffset? createdAt = null) : base(id, createdAt)
    {
        if (tenantId == Guid.Empty) throw new Exceptions.ValidationException(nameof(TenantId), "TenantId is required.");
        TenantId = tenantId;
    }

    public Guid TenantId { get; private set; }
}
