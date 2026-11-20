WebApplication app = DefaultScatteredWebApplication.Create(args, webappBuilder =>
{
    webappBuilder.Services.AddServiceBusDomainPublisher(webappBuilder.Configuration);
    
});


DefaultScatteredWebApplication.Run(app);