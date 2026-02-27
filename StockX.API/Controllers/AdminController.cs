using Microsoft.AspNetCore.Mvc;
using StockX.Core.DTOs.Admin;
using StockX.Core.DTOs.Common;
using StockX.Core.Services.Interfaces;

namespace StockX.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserListItem>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminService.GetUsersAsync(
            page,
            limit,
            search,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult<AdminUserDetail>> GetUserDetail(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var detail = await _adminService.GetUserDetailAsync(userId, cancellationToken);

        if (detail is null)
        {
            return NotFound();
        }

        return Ok(detail);
    }
}

