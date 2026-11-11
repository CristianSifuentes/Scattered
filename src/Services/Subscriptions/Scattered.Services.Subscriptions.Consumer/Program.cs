using Scattered.Services.Subscriptions.Consumer.Handler;

WebApplication app = DefaultScatteredWebApplication.Create(args, x =>
{
    x.Services.AddScoped<IDependenciaTest, DependenciaTest>();
    x.Services.AddHandlersInAssembly<SubscriptionHandler>();
    x.Services.AddServiceBusIntegrationConsumer(x.Configuration);
});


DefaultScatteredWebApplication.Run(app);