using Scattered.Services.Orders.Dto;

namespace Scattered.Services.Orders.Consumer.Handler;

public class OrderCreatedHandler : IDomainMessageHandler<OrderResponse>
{
    public Task Handle(DomainMessage<OrderResponse> message, CancellationToken cancelToken = default(CancellationToken))
    {
        //Refactored for simplicity while doing the videos.
        Console.WriteLine($"Order {message.Content.OrderId} created");
        //TODO: create order flow
        return Task.CompletedTask;
    }
}