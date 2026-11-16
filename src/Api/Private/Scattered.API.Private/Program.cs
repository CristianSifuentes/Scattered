WebApplication app = DefaultScatteredWebApplication.Create();
app.MapGet("/", () => "Hello World!");
DefaultScatteredWebApplication.Run(app);
