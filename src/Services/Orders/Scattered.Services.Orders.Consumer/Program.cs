using Scattered.Services.Orders.BusinessLogic;
using Scattered.Services.Orders.Consumer.Handler;

WebApplication app = DefaultScatteredWebApplication.Create(args, builder =>
{
    builder.Services.AddProductService(builder.Configuration);

    builder.Services.AddHandlersInAssembly<OrderCreatedHandler>();
    builder.Services.AddServiceBusDomainConsumer(builder.Configuration);
    builder.Services.AddServiceBusIntegrationConsumer(builder.Configuration);
});


DefaultScatteredWebApplication.Run(app);