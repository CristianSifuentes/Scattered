using Scattered.Services.Subscriptions.Dtos;


namespace Scattered.Services.Subscriptions.Consumer.Handler;

public class SubscriptionHandler : IIntegrationMessageHandler<SubscriptionDto>
{
    public Task Handle(IntegrationMessage<SubscriptionDto> message, CancellationToken cancelToken = default(CancellationToken))
    {
       Console.WriteLine($"Email {message.Content.Email} successfully subscribed.");
       return Task.CompletedTask;
    }
}