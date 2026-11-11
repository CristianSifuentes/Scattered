using Scattered.Shared.Setup.API.Key;
using Scattered.Shared.Setup.API.RateLimiting;

WebApplication app = DefaultScatteredWebApplication.Create(args, webappBuilder =>
{
    webappBuilder.Services.AddReverseProxy()
        .LoadFromConfig(webappBuilder.Configuration.GetSection("ReverseProxy"));

    webappBuilder.Services.AddApiToken(webappBuilder.Configuration);
    webappBuilder.Services.AddRateLimiter(_ => { });
});

app.UseApiTokenMiddleware();
app.UseRateLimiter();
app.MapGet("/", () => "Hello World!");
app.MapGet("/rate-limiting-test", () =>
{
    return "Hello World!";
}).RequireRateLimiting(new ScatteredRateLimiterPolicy());

app.MapReverseProxy();

DefaultScatteredWebApplication.Run(app);