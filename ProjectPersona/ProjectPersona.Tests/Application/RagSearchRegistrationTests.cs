using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectPersona.Application;
using ProjectPersona.Application.Services.RagSearchService;
using ProjectPersona.RagTextSearch;

namespace ProjectPersona.Tests.Application;

public class RagSearchRegistrationTests
{
    [Fact]
    public void RagSearchEnabled_DefaultsToTrue()
    {
        Assert.True(new AppSettings().RagSearchEnabled);
    }

    [Fact]
    public void AddRagSearch_RegistersSearchStateAndWarmup()
    {
        var services = new ServiceCollection().AddRagSearch();

        Assert.Contains(services, d => d.ServiceType == typeof(RagSearchService));
        Assert.Contains(services, d => d.ServiceType == typeof(RagIndexStateHolder));
        Assert.Contains(services, d => d.ServiceType == typeof(IVectorStore));
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(RagIndexWarmupService));
    }
}
