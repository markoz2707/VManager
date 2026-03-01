using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher _hasher;
    private readonly JwtTokenService _jwt;
    private readonly LdapAuthService _ldap;
    private readonly PermissionService _permissionService;
    private readonly AuditLogService _audit;

    public AuthController(
        AppDbContext db,
        PasswordHasher hasher,
        JwtTokenService jwt,
        LdapAuthService ldap,
        PermissionService permissionService,
        AuditLogService audit)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
        _ldap = ldap;
        _permissionService = permissionService;
        _audit = audit;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Username and password are required." });
        }

        if (_ldap.IsEnabled)
        {
            if (_ldap.ValidateCredentials(request.Username, request.Password, out var error))
            {
                // For LDAP users, find or create a local account to link roles
                var ldapUser = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Username == request.Username);
                if (ldapUser != null)
                {
                    var roles = await _permissionService.GetUserRoleNamesAsync(ldapUser.Id);
                    var permissions = await _permissionService.GetUserPermissionsAsync(ldapUser.Id);
                    var token = _jwt.CreateToken(ldapUser.Id, request.Username, roles, permissions);

                    ldapUser.LastLoginUtc = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync();

                    await _audit.WriteAsync(request.Username, "login", "auth=ldap");
                    return Ok(new { token, auth = "ldap", roles });
                }

                // LDAP user without local account - use legacy token
                var legacyToken = _jwt.CreateToken(request.Username, "Operator");
                await _audit.WriteAsync(request.Username, "login", "auth=ldap");
                return Ok(new { token = legacyToken, auth = "ldap" });
            }

            await _audit.WriteAsync(request.Username, "login_failed", "auth=ldap");
            return Unauthorized(new { error = "LDAP authentication failed.", details = error });
        }

        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user == null || !_hasher.Verify(request.Password, user.PasswordHash))
        {
            await _audit.WriteAsync(request.Username, "login_failed", "auth=local");
            return Unauthorized(new { error = "Invalid credentials." });
        }

        if (!user.IsActive)
        {
            await _audit.WriteAsync(request.Username, "login_failed", "auth=local, account disabled");
            return Unauthorized(new { error = "Account is disabled." });
        }

        // Load RBAC roles and permissions
        var userRoles = await _permissionService.GetUserRoleNamesAsync(user.Id);
        var userPermissions = await _permissionService.GetUserPermissionsAsync(user.Id);

        string jwtToken;
        if (userRoles.Any())
        {
            jwtToken = _jwt.CreateToken(user.Id, user.Username, userRoles, userPermissions);
        }
        else
        {
            // Fallback to legacy role field if no RBAC roles assigned
            jwtToken = _jwt.CreateToken(user.Username, user.Role);
        }

        user.LastLoginUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.WriteAsync(user.Username, "login", "auth=local");
        return Ok(new { token = jwtToken, auth = "local", roles = userRoles });
    }
}

public record LoginRequest(string Username, string Password);
