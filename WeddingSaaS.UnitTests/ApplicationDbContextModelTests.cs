using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WeddingSaaS.Domain.Entities;
using WeddingSaaS.Domain.ValueObjects;
using WeddingSaaS.Infrastructure.Identity;
using WeddingSaaS.Infrastructure.Persistence.Context;

namespace WeddingSaaS.UnitTests.Persistence;

public sealed class ApplicationDbContextModelTests
{
    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql("Host=localhost;Database=model_only;Username=not_used")
        .Options);

    [Fact]
    public void Maps_domain_and_identity_tables_to_public_snake_case()
    {
        using var context = CreateContext();
        var model = context.Model;
        AssertTable<Tenant>(model, "tenants");
        AssertTable<TenantMembership>(model, "tenant_memberships");
        AssertTable<TenantInvitation>(model, "tenant_invitations");
        AssertTable<Wedding>(model, "weddings");
        AssertTable<WeddingSettings>(model, "wedding_settings");
        AssertTable<Venue>(model, "venues");
        AssertTable<WeddingFunction>(model, "wedding_functions");
        AssertTable<ApplicationUser>(model, "asp_net_users");
        AssertTable<IdentityRole<Guid>>(model, "asp_net_roles");
        AssertTable<IdentityUserRole<Guid>>(model, "asp_net_user_roles");

        var table = StoreObjectIdentifier.Table("weddings", "public");
        Assert.Equal("start_date", model.FindEntityType(typeof(Wedding))!.FindProperty(nameof(Wedding.StartDate))!.GetColumnName(table));
        Assert.Equal("date", model.FindEntityType(typeof(Wedding))!.FindProperty(nameof(Wedding.StartDate))!.GetColumnType());
    }

    [Fact]
    public void Converts_value_objects_to_normalized_provider_strings()
    {
        using var context = CreateContext();
        var tenant = context.Model.FindEntityType(typeof(Tenant))!;
        var slugConverter = tenant.FindProperty(nameof(Tenant.Slug))!.GetValueConverter()!;
        var emailConverter = tenant.FindProperty(nameof(Tenant.Email))!.GetValueConverter()!;
        var phoneConverter = tenant.FindProperty(nameof(Tenant.Phone))!.GetValueConverter()!;
        var currencyConverter = context.Model.FindEntityType(typeof(WeddingSettings))!.FindProperty(nameof(WeddingSettings.CurrencyCode))!.GetValueConverter()!;

        Assert.Equal("garden-party", slugConverter.ConvertToProvider(TenantSlug.Create("Garden_Party")));
        Assert.Equal("owner@example.com", emailConverter.ConvertToProvider(EmailAddress.Create("OWNER@Example.com")));
        Assert.Equal("+919876543210", phoneConverter.ConvertToProvider(PhoneNumber.Create("+91 98765 43210")));
        Assert.Equal("INR", currencyConverter.ConvertToProvider(CurrencyCode.Create("inr")));
        Assert.Equal(EmailAddress.Create("owner@example.com"), emailConverter.ConvertFromProvider("owner@example.com"));
    }

    [Fact]
    public void Defines_identity_role_and_composite_tenant_integrity_constraints()
    {
        using var context = CreateContext();
        var model = context.Model;
        var membership = model.FindEntityType(typeof(TenantMembership))!;
        var wedding = model.FindEntityType(typeof(Wedding))!;
        var venue = model.FindEntityType(typeof(Venue))!;
        var function = model.FindEntityType(typeof(WeddingFunction))!;
        var settings = model.FindEntityType(typeof(WeddingSettings))!;

        var invitations = model.FindEntityType(typeof(TenantInvitation))!;
        Assert.Contains(invitations.GetIndexes(), x => x.Properties.Select(p => p.Name).SequenceEqual([nameof(TenantInvitation.Status)]));
        Assert.Contains(membership.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual([nameof(TenantMembership.TenantId), nameof(TenantMembership.UserId)]));
        Assert.Contains(wedding.GetKeys(), x => x.Properties.Select(p => p.Name).SequenceEqual([nameof(Wedding.Id), nameof(Wedding.TenantId)]));
        Assert.Contains(venue.GetKeys(), x => x.Properties.Select(p => p.Name).SequenceEqual([nameof(Venue.Id), nameof(Venue.TenantId), nameof(Venue.WeddingId)]));
        Assert.Contains(settings.GetForeignKeys(), x => x.Properties.Select(p => p.Name).SequenceEqual([nameof(WeddingSettings.WeddingId), nameof(WeddingSettings.TenantId)]));
        Assert.Contains(function.GetForeignKeys(), x => x.Properties.Select(p => p.Name).SequenceEqual([nameof(WeddingFunction.VenueId), nameof(WeddingFunction.TenantId), nameof(WeddingFunction.WeddingId)]));
        var designModel = context.GetService<IDesignTimeModel>().Model;
        Assert.Equal(4, designModel.FindEntityType(typeof(IdentityRole<Guid>))!.GetSeedData().Count());
        Assert.All(model.GetEntityTypes().Where(x => typeof(WeddingSaaS.Domain.Common.BaseEntity).IsAssignableFrom(x.ClrType)), x => Assert.NotEmpty(x.GetDeclaredQueryFilters()));
    }

    private static void AssertTable<TEntity>(IModel model, string expected)
    {
        var entity = model.FindEntityType(typeof(TEntity))!;
        Assert.Equal(expected, entity.GetTableName());
        Assert.Equal("public", entity.GetSchema());
    }
}

