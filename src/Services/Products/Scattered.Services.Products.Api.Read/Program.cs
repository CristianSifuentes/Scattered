using Scattered.Services.Products.BusinessLogic.DataAccess;

WebApplication app = DefaultScatteredWebApplication.Create(args, builder =>
{
    builder.Services.AddScatteredMongoDbConnectionProvider(builder.Configuration)
        .AddScoped<IProductsReadStore, ProductsReadStore>();
});


app.MapGet("product/{productId}", async (int productId, IProductsReadStore readStore)
    => await readStore.GetFullProduct(productId)); //TODO: result struct gives an error on minimal api?


DefaultScatteredWebApplication.Run(app);