using Scattered.Services.Orders.BusinessLogic.Services.External;
using Scattered.Services.Products.Dtos;

namespace Scattered.Services.Orders.Consumer.Handler;

public class ProductModifierHandler : IIntegrationMessageHandler<ProductUpdated>
{
    private readonly IProductNameService _productNameService;

    public ProductModifierHandler(IProductNameService productNameService)
    {
        _productNameService = productNameService;
    }

    public async Task Handle(IntegrationMessage<ProductUpdated> message, CancellationToken cancelToken = default(CancellationToken))
    {
        await _productNameService.SetProductName(message.Content.ProductId, message.Content.Details.Name, cancelToken);
    }
}