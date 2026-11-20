using Scattered.Services.Orders.BusinessLogic.Data.External;
using Scattered.Services.Orders.BusinessLogic.Services.External;
using Scattered.Shared.Setup.Databases;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Scattered.Services.Orders.BusinessLogic;

public static class OrdersBusinessLogicDependencyInjection
{
    public static void AddProductService(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        serviceCollection.AddScatteredMongoDbConnectionProvider(configuration, "productStore");
        serviceCollection.AddScoped<IProductRepository, ProductRepository>();
        serviceCollection.AddScoped<IProductNameService, ProductNameService>();
        serviceCollection.AddHttpClient();
        //For now we do not need redis, as is only for local, in prod I recommend redis.
        serviceCollection.AddDistributedMemoryCache();
    }
    
    
}