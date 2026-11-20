using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Scattered_Services_Products_Api_Read>("productsread");
builder.AddProject<Scattered_Services_Products_Api_Write>("productswrite");
builder.AddProject<Scattered_Services_Products_Consumer>("productsconsumer");


builder.Build().Run();