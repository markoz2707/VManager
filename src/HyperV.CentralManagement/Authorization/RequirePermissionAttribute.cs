using Microsoft.AspNetCore.Authorization;

namespace HyperV.CentralManagement.Authorization;

/// <summary>
/// Require a specific permission (resource + action) to access an endpoint.
/// Usage: [RequirePermission("vm", "power")]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string resource, string action)
        : base(policy: $"Permission:{resource}:{action}")
    {
    }
}
