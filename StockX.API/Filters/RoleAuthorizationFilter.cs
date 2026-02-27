using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace StockX.API.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RoleAuthorizationFilter : Attribute, IAuthorizationFilter
{
    public RoleAuthorizationFilter(params string[] roles)
    {
        Roles = roles ?? Array.Empty<string>();
    }

    public IReadOnlyList<string> Roles { get; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (Roles.Count == 0)
        {
            return;
        }

        var user = context.HttpContext.User;

        if (user?.Identity is null || !user.Identity.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var hasRole = Roles.Any(role => user.IsInRole(role));

        if (!hasRole)
        {
            context.Result = new ForbidResult();
        }
    }
}

