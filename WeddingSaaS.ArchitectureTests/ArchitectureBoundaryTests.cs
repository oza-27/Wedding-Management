using WeddingSaaS.API.Controllers;
using WeddingSaaS.Application.Abstractions.Authentication;
using WeddingSaaS.Domain.Entities;
using WeddingSaaS.Infrastructure.Services;

namespace WeddingSaaS.ArchitectureTests;

public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void Inner_layers_do_not_reference_outer_layers()
    {
        var domain = typeof(Tenant).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
        var application = typeof(IAuthenticationService).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
        var infrastructure = typeof(AuthenticationService).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("WeddingSaaS.Application", domain);
        Assert.DoesNotContain("WeddingSaaS.Infrastructure", domain);
        Assert.DoesNotContain("WeddingSaaS.API", domain);
        Assert.DoesNotContain("WeddingSaaS.Infrastructure", application);
        Assert.DoesNotContain("WeddingSaaS.API", application);
        Assert.Contains("WeddingSaaS.Application", infrastructure);
        Assert.Contains("WeddingSaaS.Domain", infrastructure);
        Assert.DoesNotContain("WeddingSaaS.API", infrastructure);
    }

    [Fact]
    public void Authentication_controller_depends_on_application_contract()
    {
        var constructor = typeof(AuthController).GetConstructors().Single();
        Assert.Equal(typeof(IAuthenticationService), constructor.GetParameters().Single().ParameterType);
    }
}
