using Scattered.Services.Orders.Consumer.Handler;

WebApplication app = DefaultScatteredWebApplication.Create(args, x =>
{
    x.Services.AddHandlersInAssembly<OrderCreatedHandler>();
    x.Services.AddServiceBusDomainConsumer(x.Configuration);
});


DefaultScatteredWebApplication.Run(app);