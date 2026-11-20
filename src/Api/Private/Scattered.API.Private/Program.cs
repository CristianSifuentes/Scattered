using Scattered.Shared.Communication.RabbitMQ;
using Scattered.Shared.Secrets;

WebApplication app = DefaultScatteredWebApplication.Create(webappBuilder =>
{
    webappBuilder.Services.AddReverseProxy()
        .LoadFromConfig(webappBuilder.Configuration.GetSection("ReverseProxy"));
});
app.MapGet("/", () => "Hello World!");

app.MapReverseProxy();
DefaultScatteredWebApplication.Run(app);
