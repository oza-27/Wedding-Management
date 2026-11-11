namespace WeddingSaaS.Domain.Common;

/// <summary>Base for domain objects. IDs are supplied by the creating application boundary; creation never generates one implicitly.</summary>
public abstract class BaseEntity
{
    // EF Core materialization only. Public creation paths always require an application-supplied ID.
    protected BaseEntity() { }

    protected BaseEntity(Guid id, DateTimeOffset? createdAt = null)
    {
        if (id == Guid.Empty) throw new Exceptions.ValidationException("Id", "An entity ID must be supplied and cannot be empty.");
        Id = id;
        CreatedAt = (createdAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }

    public void SetCreator(Guid? createdBy) => CreatedBy = createdBy;

    protected void RecordUpdate(Guid? updatedBy, DateTimeOffset? at = null)
    {
        UpdatedBy = updatedBy;
        UpdatedAt = (at ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }

    public void MarkDeleted(Guid? deletedBy = null, DateTimeOffset? at = null)
    {
        if (IsDeleted) return;
        IsDeleted = true;
        RecordUpdate(deletedBy, at);
    }
}
