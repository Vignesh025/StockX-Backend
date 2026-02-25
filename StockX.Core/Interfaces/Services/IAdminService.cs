using StockX.Core.DTOs.Admin;
using StockX.Core.DTOs.Common;
using StockX.Core.DTOs.Portfolio;
using StockX.Core.Entities;
using StockX.Core.Enums;

namespace StockX.Core.Services.Interfaces;

public interface IAdminService
{
    Task<PagedResult<AdminUserListItem>> GetUsersAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetail?> GetUserDetailAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

