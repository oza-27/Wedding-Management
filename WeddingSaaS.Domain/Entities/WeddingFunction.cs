using WeddingSaaS.Domain.Common;
using WeddingSaaS.Domain.Enums;
using WeddingSaaS.Domain.Exceptions;

namespace WeddingSaaS.Domain.Entities;

public sealed class WeddingFunction : WeddingEntity
{
    // EF Core materialization only.
    private WeddingFunction() : base() { Name = string.Empty; }

    public WeddingFunction(Guid id, Wedding wedding, string name, DateOnly functionDate, TimeOnly? startTime = null, TimeOnly? endTime = null, Venue? venue = null, string? description = null, DateTimeOffset? createdAt = null)
        : base(id, wedding?.TenantId ?? Guid.Empty, wedding?.Id ?? Guid.Empty, createdAt)
    {
        ArgumentNullException.ThrowIfNull(wedding);
        Name = Required(name);
        if (functionDate < wedding.StartDate || functionDate > wedding.EndDate)
            throw new ValidationException(nameof(FunctionDate), "Function date must be within the wedding date range.");
        if (startTime.HasValue && endTime.HasValue && endTime.Value <= startTime.Value)
            throw new ValidationException(nameof(EndTime), "Function end time must be after its start time.");
        if (venue is not null && (venue.TenantId != wedding.TenantId || venue.WeddingId != wedding.Id))
            throw new BusinessRuleException("Venue must belong to the same tenant and wedding as the function.");
        FunctionDate = functionDate;
        StartTime = startTime;
        EndTime = endTime;
        VenueId = venue?.Id;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Status = WeddingFunctionStatus.Draft;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public DateOnly FunctionDate { get; private set; }
    public TimeOnly? StartTime { get; private set; }
    public TimeOnly? EndTime { get; private set; }
    public Guid? VenueId { get; private set; }
    public WeddingFunctionStatus Status { get; private set; }

    public void Publish()
    {
        if (Status != WeddingFunctionStatus.Draft) throw new BusinessRuleException("Only a draft function can be published.");
        Status = WeddingFunctionStatus.Published;
    }
    public void Complete()
    {
        if (Status == WeddingFunctionStatus.Cancelled) throw new BusinessRuleException("A cancelled function cannot be completed.");
        Status = WeddingFunctionStatus.Completed;
    }
    public void Cancel()
    {
        if (Status == WeddingFunctionStatus.Completed) throw new BusinessRuleException("A completed function cannot be cancelled.");
        Status = WeddingFunctionStatus.Cancelled;
    }
    private static string Required(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ValidationException(nameof(Name), "Function name is required.");
        return normalized;
    }
}
