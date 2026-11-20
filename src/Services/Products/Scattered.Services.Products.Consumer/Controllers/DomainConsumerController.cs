using Scattered.Shared.Communication.Consumer.Host;
using Scattered.Shared.Communication.Consumer.Manager;
using Microsoft.AspNetCore.Mvc;

namespace Scattered.Services.Products.Consumer.Controllers;

[ApiController]
[Route("[controller]")]
public class DomainConsumerController : ConsumerController<DomainMessage>
{
    public DomainConsumerController(IConsumerManager<DomainMessage> consumerManager) : base(consumerManager)
    {
    }
}