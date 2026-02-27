using Microsoft.EntityFrameworkCore;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Core.Interfaces.Repositories;
using StockX.Infrastructure.Persistence.Context;

namespace StockX.Infrastructure.Persistence.Repositories;

public sealed class PaymentIntentRepository : Repository<PaymentIntent>, IPaymentIntentRepository
{
    public PaymentIntentRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public Task<PaymentIntent?> GetByIntentIdAsync(
        string intentId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.PaymentIntents
            .FirstOrDefaultAsync(p => p.IntentId == intentId, cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentIntent>> GetByUserAndStatusAsync(
        Guid userId,
        PaymentIntentStatus status,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.PaymentIntents
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.Status == status)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

