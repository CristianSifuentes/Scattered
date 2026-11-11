using Scattered.Services.Orders.Aggregates;
using Scattered.Services.Orders.Data;
using Scattered.Services.Orders.Events;

namespace Scattered.Services.Orders.Services;

public interface IOrderDeliveredService
{
    Task<bool> Execute(Guid orderId, CancellationToken cancellationToken = default(CancellationToken));
}

public class OrderDeliveredService : IOrderDeliveredService
{
    private readonly IOrderRepository _orderRepository;

    public OrderDeliveredService(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }


    public async Task<bool> Execute(Guid orderId, CancellationToken cancellationToken = default(CancellationToken))
    { 
        
        OrderDetails orderDetails =  await _orderRepository.GetById(orderId, cancellationToken);
        orderDetails.Apply(new OrderCompleted());
        await _orderRepository.Save(orderDetails, cancellationToken);
        return true;
    }
}