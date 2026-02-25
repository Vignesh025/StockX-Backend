using StockX.Core.Enums;

namespace StockX.Core.DTOs.Admin;

public sealed record AdminUserListItem(
    Guid UserId,
    string Name,
    string Email,
    UserRole Role,
    bool IsActive,
    decimal WalletBalance);

