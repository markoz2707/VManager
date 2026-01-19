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

    private readonly AuditLogService _audit;

    public AuthController(AppDbContext db, PasswordHasher hasher, JwtTokenService jwt, LdapAuthService ldap, AuditLogService audit)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
        _ldap = ldap;
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
                var token = _jwt.CreateToken(request.Username, "Operator");
                await _audit.WriteAsync(request.Username, "login", "auth=ldap");
                return Ok(new { token, auth = "ldap" });
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

        var jwtToken = _jwt.CreateToken(user.Username, user.Role);
        await _audit.WriteAsync(user.Username, "login", "auth=local");
        return Ok(new { token = jwtToken, auth = "local" });
    }
}

public record LoginRequest(string Username, string Password);
