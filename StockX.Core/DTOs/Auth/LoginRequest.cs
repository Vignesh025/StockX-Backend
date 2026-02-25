namespace StockX.Core.DTOs.Auth;

public sealed record LoginRequest(
    string Email,
    string Password);

