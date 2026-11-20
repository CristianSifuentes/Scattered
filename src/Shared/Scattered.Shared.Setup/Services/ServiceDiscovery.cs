using Microsoft.Extensions.Configuration;

namespace Scattered.Shared.Setup.Services;

public static class ServiceDiscovery
{
    public static void AddServiceDiscovery(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        serviceCollection.AddDiscovery(configuration); 
    }
}