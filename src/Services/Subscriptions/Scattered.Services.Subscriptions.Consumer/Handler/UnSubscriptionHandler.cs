using Scattered.Services.Subscriptions.Dtos;

namespace Scattered.Services.Subscriptions.Consumer.Handler;

public class UnSubscriptionHandler : IIntegrationMessageHandler<UnSubscriptionDto>
{
    public Task Handle(IntegrationMessage<UnSubscriptionDto> message, CancellationToken cancelToken = default(CancellationToken))
    {
        Console.WriteLine($"the email {message.Content.Email} has unsubscribed.");
        //TODO: Full use case
        return Task.CompletedTask;
    }
}