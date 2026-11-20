using Scattered.Services.Orders.Aggregates;
using Scattered.Services.Orders.Data;
using Scattered.Services.Orders.Services;

WebApplication app = DefaultScatteredWebApplication.Create(args, webappBuilder =>
{
    MongoMapping.RegisterClasses();
    webappBuilder.Services.AddServiceBusDomainPublisher(webappBuilder.Configuration);
    webappBuilder.Services.AddScatteredMongoDbConnectionProvider();
    webappBuilder.Services.AddEventSourcing(webappBuilder.Configuration);
    webappBuilder.Services.AddScoped<IOrderRepository, OrderRepository>();
    webappBuilder.Services.AddScoped<ICreateOrderService, CreateOrderService>();
    webappBuilder.Services.AddScoped<IGetOrderService, GetOrderService>();
    webappBuilder.Services.AddScoped<IOrderPaidService, OrderPaidService>();
    webappBuilder.Services.AddScoped<IOrderDispatchedService, OrderDispatchedService>();
    webappBuilder.Services.AddScoped<IOrderDeliveredService, OrderDeliveredService>();
});


DefaultScatteredWebApplication.Run(app);