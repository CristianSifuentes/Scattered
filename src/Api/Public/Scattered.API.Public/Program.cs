using Scattered.Services.Subscriptions.Dtos;

WebApplication app = DefaultScatteredWebApplication.Create(webappBuilder =>
{
    webappBuilder.Services.AddReverseProxy()
        .LoadFromConfig(webappBuilder.Configuration.GetSection("ReverseProxy"));
    webappBuilder.Services.AddServiceBusIntegrationPublisher(webappBuilder.Configuration);
});

app.MapGet("/", () => "Hello World!");

app.MapPost("/subscribe", async (SubscriptionDto subscriptionDto) =>
{
    IIntegrationMessagePublisher publisher = app.Services.GetRequiredService<IIntegrationMessagePublisher>();
    await publisher.Publish(subscriptionDto, routingKey: "subscription");
});

app.MapReverseProxy();


DefaultScatteredWebApplication.Run(app);