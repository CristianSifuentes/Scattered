using Scattered.Services.Subscriptions.Consumer.Handler;
using Scattered.Shared.Api;
using Scattered.Shared.Communication.Consumer.Handler;
using Scattered.Shared.Setup;

WebApplication app = DefaultScatteredWebApplication.Create(x =>
{
    x.Services.AddHandlers(new List<IMessageHandler>()
    {
        new SubscriptionHandler(),
        new UnSubscriptionHandler()
    });
    x.Services.AddServiceBusIntegrationConsumer(x.Configuration);
});


DefaultScatteredWebApplication.Run(app);