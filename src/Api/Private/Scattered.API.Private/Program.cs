using Scattered.Shared.Setup.API.Key;

WebApplication app = DefaultScatteredWebApplication.Create(args, webappBuilder =>
{
    webappBuilder.Services.AddReverseProxy()
        .LoadFromConfig(webappBuilder.Configuration.GetSection("ReverseProxy"));
    
    webappBuilder.Services.AddApiToken(webappBuilder.Configuration);
  
});
app.MapGet("/", () => "Hello World!");

app.MapReverseProxy();
app.UseApiTokenMiddleware();
DefaultScatteredWebApplication.Run(app);
