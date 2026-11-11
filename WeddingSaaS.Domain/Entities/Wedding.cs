using WeddingSaaS.Domain.Common;
using WeddingSaaS.Domain.Enums;
using WeddingSaaS.Domain.Exceptions;

namespace WeddingSaaS.Domain.Entities;

public sealed class Wedding : TenantEntity
{
    // EF Core materialization only.
    private Wedding() : base() { WeddingName = string.Empty; BrideName = string.Empty; GroomName = string.Empty; }

    public Wedding(Guid id, Guid tenantId, string weddingName, string brideName, string groomName, DateOnly startDate, DateOnly endDate, DateTimeOffset? createdAt = null)
        : base(id, tenantId, createdAt)
    {
        WeddingName = Required(weddingName, nameof(WeddingName));
        BrideName = Required(brideName, nameof(BrideName));
        GroomName = Required(groomName, nameof(GroomName));
        if (startDate > endDate) throw new ValidationException(nameof(EndDate), "Wedding start date cannot be after its end date.");
        StartDate = startDate;
        EndDate = endDate;
        Status = WeddingStatus.Draft;
    }

    public string WeddingName { get; private set; }
    public string BrideName { get; private set; }
    public string GroomName { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public WeddingStatus Status { get; private set; }

    public void UpdateDetails(string weddingName, string brideName, string groomName, DateOnly startDate, DateOnly endDate, Guid? updatedBy = null, DateTimeOffset? at = null)
    {
        if (Status is WeddingStatus.Completed or WeddingStatus.Cancelled) throw new BusinessRuleException("A completed or cancelled wedding cannot be changed.");
        if (startDate > endDate) throw new ValidationException(nameof(EndDate), "Wedding start date cannot be after its end date.");
        WeddingName = Required(weddingName, nameof(WeddingName));
        BrideName = Required(brideName, nameof(BrideName));
        GroomName = Required(groomName, nameof(GroomName));
        StartDate = startDate;
        EndDate = endDate;
        RecordUpdate(updatedBy, at);
    }

    public void Activate(Guid? updatedBy = null, DateTimeOffset? at = null)
    {
        if (Status != WeddingStatus.Draft) throw new BusinessRuleException("Only a draft wedding can be activated.");
        Status = WeddingStatus.Active;
        RecordUpdate(updatedBy, at);
    }

    public void Complete(Guid? updatedBy = null, DateTimeOffset? at = null)
    {
        if (Status != WeddingStatus.Active) throw new BusinessRuleException("Only an active wedding can be completed.");
        Status = WeddingStatus.Completed;
        RecordUpdate(updatedBy, at);
    }

    public void Cancel(Guid? updatedBy = null, DateTimeOffset? at = null)
    {
        if (Status is WeddingStatus.Completed or WeddingStatus.Cancelled) throw new BusinessRuleException("A completed or cancelled wedding cannot be cancelled.");
        Status = WeddingStatus.Cancelled;
        RecordUpdate(updatedBy, at);
    }
private static string Required(string value, string property)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ValidationException(property, $"{property} is required.");
        return normalized;
    }
}

