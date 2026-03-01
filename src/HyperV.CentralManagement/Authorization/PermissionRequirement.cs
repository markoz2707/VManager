using System.Security.Claims;
using HyperV.CentralManagement.Services;
using Microsoft.AspNetCore.Authorization;

namespace HyperV.CentralManagement.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public string Resource { get; }
    public string Action { get; }

    public PermissionRequirement(string resource, string action)
    {
        Resource = resource;
        Action = action;
    }
}

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceProvider _serviceProvider;

    public PermissionHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst("userId");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return;
        }

        // Check embedded permission claims first (fast path)
        var permissionClaims = context.User.FindAll("permission");
        var requiredPermission = $"{requirement.Resource}:{requirement.Action}";

        if (permissionClaims.Any(c => c.Value == requiredPermission || c.Value == $"{requirement.Resource}:*" || c.Value == "*:*"))
        {
            context.Succeed(requirement);
            return;
        }

        // Fallback to DB check (for long-lived tokens)
        using var scope = _serviceProvider.CreateScope();
        var permissionService = scope.ServiceProvider.GetRequiredService<PermissionService>();

        if (await permissionService.HasPermissionAsync(userId, requirement.Resource, requirement.Action))
        {
            context.Succeed(requirement);
        }
    }
}
