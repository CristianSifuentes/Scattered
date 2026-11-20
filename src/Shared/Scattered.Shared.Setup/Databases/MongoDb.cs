using Scattered.Shared.Databases.MongoDb;
using Microsoft.Extensions.Configuration;

namespace Scattered.Shared.Setup.Databases;

public static class MongoDb
{
    public static IServiceCollection AddScatteredMongoDbConnectionProvider(this IServiceCollection serviceCollection,
        IConfiguration configuration, string name = "mongodb")
    {
        return serviceCollection
            .AddMongoDbConnectionProvider()
            .AddMongoDbDatabaseConfiguration(configuration)
            .AddMongoHealthCheck(name);
    }
}