using Scattered.Shared.EventSourcing;
using Microsoft.Extensions.Configuration;

namespace Scattered.Shared.Setup.Services;

public static class EventSourcing
{
    public static void AddEventSourcing(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        serviceCollection.AddMongoEventSourcing(configuration);
    }
}