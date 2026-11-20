using Scattered.Services.Orders.Consumer.Handler;

WebApplication app = DefaultScatteredWebApplication.Create(x =>
{
    x.Services.AddHandlers(new List<IMessageHandler>()
    {
        new OrderCreatedHandler(),
    });
    x.Services.AddServiceBusDomainConsumer(x.Configuration);
});


DefaultScatteredWebApplication.Run(app);