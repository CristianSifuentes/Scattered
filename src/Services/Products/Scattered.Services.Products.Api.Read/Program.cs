using Scattered.Services.Products.BusinessLogic.DataAccess;

WebApplication app = DefaultScatteredWebApplication.Create(args, builder =>
{
    builder.Services.AddScatteredMongoDbConnectionProvider()
        .AddScoped<IProductsReadStore, ProductsReadStore>();
});


app.MapGet("product/{productId}", async ( int productId, IProductsReadStore readStore) 
    => await readStore.GetFullProduct(productId));


DefaultScatteredWebApplication.Run(app);