using WeddingSaaS.Domain.Entities;
using WeddingSaaS.Domain.Enums;
using WeddingSaaS.Domain.Exceptions;

namespace WeddingSaaS.UnitTests.Domain;

public sealed class WeddingDomainTests
{
    [Fact]
    public void Creates_wedding_in_draft_for_a_required_tenant()
    {
        var tenantId = Guid.NewGuid();
        var wedding = CreateWedding(tenantId: tenantId);
        Assert.Equal(tenantId, wedding.TenantId);
        Assert.Equal(WeddingStatus.Draft, wedding.Status);
    }

    [Fact]
    public void Rejects_empty_wedding_name_and_invalid_date_range()
    {
        Assert.Throws<ValidationException>(() => new Wedding(Guid.NewGuid(), Guid.NewGuid(), " ", "Bride", "Groom", new(2027, 1, 1), new(2027, 1, 2)));
        Assert.Throws<ValidationException>(() => new Wedding(Guid.NewGuid(), Guid.NewGuid(), "Wedding", "Bride", "Groom", new(2027, 1, 3), new(2027, 1, 2)));
        Assert.Throws<ValidationException>(() => new Wedding(Guid.NewGuid(), Guid.Empty, "Wedding", "Bride", "Groom", new(2027, 1, 1), new(2027, 1, 2)));
    }

    [Fact]
    public void Wedding_status_transitions_do_not_reopen_completed_wedding()
    {
        var wedding = CreateWedding();
        wedding.Activate();
        Assert.Equal(WeddingStatus.Active, wedding.Status);
        wedding.Complete();
        Assert.Equal(WeddingStatus.Completed, wedding.Status);
        Assert.Throws<BusinessRuleException>(() => wedding.Activate());
        Assert.Throws<BusinessRuleException>(() => wedding.UpdateDetails("Again", "Bride", "Groom", wedding.StartDate, wedding.EndDate));
        Assert.Equal(WeddingStatus.Completed, wedding.Status);
    }

    [Fact]
    public void Wedding_settings_default_currency_and_guest_portal_but_require_timezone()
    {
        var wedding = CreateWedding();
        var settings = new WeddingSettings(Guid.NewGuid(), wedding, "UTC");
        Assert.Equal("INR", settings.CurrencyCode.Value);
        Assert.False(settings.GuestPortalEnabled);
        Assert.Throws<ValidationException>(() => new WeddingSettings(Guid.NewGuid(), wedding, " "));
        Assert.Throws<ValidationException>(() => new WeddingSettings(Guid.NewGuid(), wedding, "not/a-real-zone"));
    }

    [Fact]
    public void Venue_rejects_invalid_or_partial_coordinates()
    {
        var wedding = CreateWedding();
        Assert.Throws<ValidationException>(() => new Venue(Guid.NewGuid(), wedding, "Hall", latitude: 91, longitude: 0));
        Assert.Throws<ValidationException>(() => new Venue(Guid.NewGuid(), wedding, "Hall", latitude: 12));
        Assert.Throws<ValidationException>(() => new Venue(Guid.NewGuid(), wedding, "Hall", latitude: 12, longitude: 181));
    }

    [Fact]
    public void Function_must_be_within_wedding_dates_and_have_valid_time_range()
    {
        var wedding = CreateWedding();
        var outsideDate = wedding.EndDate.AddDays(1);
        Assert.Throws<ValidationException>(() => new WeddingFunction(Guid.NewGuid(), wedding, "Reception", outsideDate));
        Assert.Throws<ValidationException>(() => new WeddingFunction(Guid.NewGuid(), wedding, "Reception", wedding.StartDate, new(20, 0), new(19, 0)));
    }

    [Fact]
    public void Function_venue_must_match_wedding_and_cancelled_function_cannot_complete()
    {
        var wedding = CreateWedding();
        var otherWedding = CreateWedding(tenantId: wedding.TenantId);
        var otherVenue = new Venue(Guid.NewGuid(), otherWedding, "Other Hall");
        Assert.Throws<BusinessRuleException>(() => new WeddingFunction(Guid.NewGuid(), wedding, "Reception", wedding.StartDate, venue: otherVenue));

        var function = new WeddingFunction(Guid.NewGuid(), wedding, "Reception", wedding.StartDate);
        function.Cancel();
        Assert.Throws<BusinessRuleException>(function.Complete);
    }

    private static Wedding CreateWedding(Guid? tenantId = null) => new(Guid.NewGuid(), tenantId ?? Guid.NewGuid(), "The Wedding", "Asha", "Arjun", new(2027, 2, 10), new(2027, 2, 12));
}

