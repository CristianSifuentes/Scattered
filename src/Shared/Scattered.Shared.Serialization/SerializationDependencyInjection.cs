using Microsoft.Extensions.DependencyInjection;

namespace Scattered.Shared.Serialization;

public static class SerializationDependencyInjection
{
    public static void AddSerializer(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<ISerializer, Serializer>();
    }
}