using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace HyperV.CentralManagement.Authorization;

/// <summary>
/// Dynamic policy provider that creates permission-based policies on demand.
/// Handles policies with format "Permission:{resource}:{action}"
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private const string PermissionPrefix = "Permission:";
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallbackProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallbackProvider.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PermissionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var parts = policyName[PermissionPrefix.Length..].Split(':');
            if (parts.Length == 2)
            {
                var policy = new AuthorizationPolicyBuilder()
                    .AddRequirements(new PermissionRequirement(parts[0], parts[1]))
                    .Build();
                return Task.FromResult<AuthorizationPolicy?>(policy);
            }
        }

        return _fallbackProvider.GetPolicyAsync(policyName);
    }
}
