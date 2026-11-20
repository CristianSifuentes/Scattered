using Scattered.Services.Products.BusinessLogic.DataAccess;
using Scattered.Services.Products.Consumer.Handlers;

WebApplication app = DefaultScatteredWebApplication.Create(args, builder =>
{
    builder.Services.AddScatteredMongoDbConnectionProvider()
        .AddScoped<IProductsReadStore, ProductsReadStore>();
    builder.Services.AddServiceBusIntegrationPublisher(builder.Configuration);
    builder.Services.AddHandlersInAssembly<ProductUpdatedHandler>();
    builder.Services.AddServiceBusDomainConsumer(builder.Configuration);
});


DefaultScatteredWebApplication.Run(app);