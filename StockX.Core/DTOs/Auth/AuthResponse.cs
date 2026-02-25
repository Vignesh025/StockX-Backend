using StockX.Core.Enums;

namespace StockX.Core.DTOs.Auth;

public sealed record AuthUserDto(
    Guid Id,
    string Name,
    string Email,
    UserRole Role);

public sealed record AuthResponse(
    string Token,
    AuthUserDto User);

