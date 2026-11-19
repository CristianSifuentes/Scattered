using Scattered.Services.Subscriptions.Dtos;
using Scattered.Shared.Communication.Consumer.Handler;
using Scattered.Shared.Communication.Messages;

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