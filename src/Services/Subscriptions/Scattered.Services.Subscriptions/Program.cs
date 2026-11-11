WebApplication app = DefaultScatteredWebApplication.Create(args, webappBuilder =>
{
    webappBuilder.Services.AddServiceBusIntegrationPublisher(webappBuilder.Configuration);
});

DefaultScatteredWebApplication.Run(app);

public partial class Program { }