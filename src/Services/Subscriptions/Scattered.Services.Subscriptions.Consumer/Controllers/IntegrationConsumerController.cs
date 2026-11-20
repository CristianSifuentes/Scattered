using Scattered.Shared.Communication.Consumer.Host;
using Scattered.Shared.Communication.Consumer.Manager;
using Microsoft.AspNetCore.Mvc;

namespace Scattered.Services.Subscriptions.Consumer.Controllers;

[ApiController]
[Route("[controller]")]
public class IntegrationConsumerController : ConsumerController<IntegrationMessage>
{
    public IntegrationConsumerController(IConsumerManager<IntegrationMessage> consumerManager) : base(consumerManager)
    {
    }
}