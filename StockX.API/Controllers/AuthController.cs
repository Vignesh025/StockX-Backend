using Microsoft.AspNetCore.Mvc;
using StockX.Core.DTOs.Auth;
using StockX.Core.Services.Interfaces;

namespace StockX.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IWalletService _walletService;

    public AuthController(
        IAuthService authService,
        IWalletService walletService)
    {
        _authService = authService;
        _walletService = walletService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<object>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _authService.RegisterAsync(
            request.Name,
            request.Email,
            request.Password,
            cancellationToken);

        return Ok(new
        {
            userId = user.UserId,
            message = "Registration successful."
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var token = await _authService.LoginAsync(
            request.Email,
            request.Password,
            cancellationToken);

        if (token is null)
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        var user = await _authService.GetByEmailAsync(request.Email, cancellationToken);

        if (user is null)
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        var authUser = new AuthUserDto(
            user.UserId,
            user.Name,
            user.Email,
            user.Role);

        return Ok(new AuthResponse(token, authUser));
    }

    [HttpGet("me")]
    public async Task<ActionResult<object>> Me(CancellationToken cancellationToken)
    {
        // Placeholder: normally derive user id from JWT.
        return Unauthorized();
    }
}

