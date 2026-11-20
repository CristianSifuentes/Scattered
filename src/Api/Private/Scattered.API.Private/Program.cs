WebApplication app = DefaultScatteredWebApplication.Create(args, webappBuilder =>
{
    webappBuilder.Services.AddReverseProxy()
        .LoadFromConfig(webappBuilder.Configuration.GetSection("ReverseProxy"));
});
app.MapGet("/", () => "Hello World!");

app.MapReverseProxy();
DefaultScatteredWebApplication.Run(app);
