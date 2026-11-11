using WeddingSaaS.Domain.Exceptions;
using WeddingSaaS.Domain.ValueObjects;

namespace WeddingSaaS.UnitTests.Domain;

public sealed class DomainValueObjectTests
{
    [Fact]
    public void Email_and_phone_values_are_normalized_and_validated()
    {
        Assert.Equal("person@example.com", EmailAddress.Create(" PERSON@Example.com ").Value);
        Assert.Throws<ValidationException>(() => EmailAddress.Create("invalid"));
        Assert.Equal("+919876543210", PhoneNumber.Create("+91 (98765) 43210").Value);
        Assert.Throws<ValidationException>(() => PhoneNumber.Create("9876543210"));
    }

    [Fact]
    public void Slug_and_currency_values_are_normalized_and_validated()
    {
        Assert.Equal("my-wedding-2027", TenantSlug.Create(" My_Wedding 2027 ").Value);
        Assert.Throws<ValidationException>(() => TenantSlug.Create("bad/slug"));
        Assert.Equal("INR", CurrencyCode.Create(" inr ").Value);
        Assert.Throws<ValidationException>(() => CurrencyCode.Create("US1"));
    }
}
