using Scattered.Services.Orders.Aggregates;
using Scattered.Shared.EventSourcing;
using Scattered.Shared.EventSourcing.EventStores;

namespace Scattered.Services.Orders.Data;

public interface IOrderRepository
{
    Task<OrderDetails> GetById(Guid id, CancellationToken cancellationToken = default(CancellationToken));
    Task<OrderDetails?> GetByIdOrDefault(Guid id, CancellationToken cancellationToken = default(CancellationToken));
    Task Save(OrderDetails orderDetails, CancellationToken cancellationToken = default(CancellationToken));
}

public class OrderRepository : AggregateRepository<OrderDetails>, IOrderRepository
{
    public OrderRepository(IEventStore eventStore) : base(eventStore)
    {
    }

    public async Task<OrderDetails> GetById(Guid id, CancellationToken cancellationToken = default(CancellationToken))
        => await GetByIdAsync(id, cancellationToken);

    public async Task<OrderDetails?> GetByIdOrDefault(Guid id,
        CancellationToken cancellationToken = default(CancellationToken))
        => await GetByIdOrDefaultAsync(id, cancellationToken);

    public async Task Save(OrderDetails orderDetails, CancellationToken cancellationToken = default(CancellationToken))
        => await SaveAsync(orderDetails, cancellationToken);
}