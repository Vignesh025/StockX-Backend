using StockX.Core.Entities;
using StockX.Core.Enums;

namespace StockX.Core.Interfaces.Repositories;

public interface IPaymentIntentRepository : IRepository<PaymentIntent>
{
    Task<PaymentIntent?> GetByIntentIdAsync(
        string intentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentIntent>> GetByUserAndStatusAsync(
        Guid userId,
        PaymentIntentStatus status,
        CancellationToken cancellationToken = default);
}

