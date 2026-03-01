using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HyperV.CentralManagement.Services;

public class JwtOptions
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}

public class JwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Legacy method - creates token with simple role string
    /// </summary>
    public string CreateToken(string username, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)
        };

        return GenerateToken(claims);
    }

    /// <summary>
    /// Creates token with userId, roles, and embedded permission claims
    /// </summary>
    public string CreateToken(Guid userId, string username, IEnumerable<string> roles, IEnumerable<PermissionInfo> permissions)
    {
        var claims = new List<Claim>
        {
            new("userId", userId.ToString()),
            new(ClaimTypes.Name, username)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var perm in permissions)
        {
            claims.Add(new Claim("permission", $"{perm.Resource}:{perm.Action}"));
        }

        return GenerateToken(claims);
    }

    private string GenerateToken(IEnumerable<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
