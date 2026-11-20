using Scattered.Services.Orders.Aggregates;
using Scattered.Services.Orders.Data;
using Scattered.Services.Orders.Dto;

namespace Scattered.Services.Orders.Services;

public interface IGetOrderService
{
    Task<OrderResponse> Execute(Guid orderId, CancellationToken cancellationToken = default(CancellationToken));
}

public class GetOrderService : IGetOrderService
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderService(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }


    public async Task<OrderResponse> Execute(Guid orderId,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        OrderDetails orderDetails = await _orderRepository.GetById(orderId, cancellationToken);
        //on a real scenario this implementation will be much bigger.
        return new OrderResponse(orderDetails.Id, orderDetails.Status.ToString(), orderDetails.Delivery, orderDetails.PaymentInformation,
            orderDetails.Products
                .Select(p => new ProductQuantityName(p.ProductId, p.Quantity, "fakeNAme"))
                .ToList());
    }
}