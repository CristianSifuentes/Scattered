using Scattered.Services.Products.BusinessLogic.DataAccess;
using Scattered.Services.Products.BusinessLogic.UseCases;

WebApplication app = DefaultScatteredWebApplication.Create(args, builder =>
{
    builder.Services.AddMySql<ProductsWriteStore>("scattered")
        .AddScoped<IProductsWriteStore, ProductsWriteStore>()
        .AddScoped<IUpdateProductDetails, UpdateProductDetails>()
        .AddScoped<ICreateProductDetails, CreateProductDetails>()
        .AddScoped<IStockApi,ProductsDependencyFakeType>() //testing purposes
        .AddScoped<IWarehouseApi, ProductsDependencyFakeType>() //testing purposes
        .AddServiceBusDomainPublisher(builder.Configuration);
});

DefaultScatteredWebApplication.Run(app);
