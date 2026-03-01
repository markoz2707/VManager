using HyperV.CentralManagement.Services;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace HyperV.CentralManagement.Tests;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _jwtTokenService;
    private readonly JwtOptions _jwtOptions;

    public JwtTokenServiceTests()
    {
        _jwtOptions = new JwtOptions
        {
            Secret = "ThisIsAVeryLongSecretKeyForTestingPurposesOnly1234567890!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationMinutes = 60
        };

        _jwtTokenService = new JwtTokenService(Options.Create(_jwtOptions));
    }

    // --- CreateToken (legacy) Tests ---

    [Fact]
    public void CreateToken_WithUsernameAndRole_ReturnsValidJwt()
    {
        // Act
        var token = _jwtTokenService.CreateToken("testuser", "Admin");

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public void CreateToken_WithUsernameAndRole_ContainsCorrectClaims()
    {
        // Act
        var token = _jwtTokenService.CreateToken("testuser", "Admin");

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.Contains(jwtToken.Claims, c => c.Type == ClaimTypes.Name && c.Value == "testuser");
        Assert.Contains(jwtToken.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public void CreateToken_WithUsernameAndRole_HasCorrectIssuer()
    {
        // Act
        var token = _jwtTokenService.CreateToken("testuser", "Admin");

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.Equal("TestIssuer", jwtToken.Issuer);
    }

    [Fact]
    public void CreateToken_WithUsernameAndRole_HasCorrectAudience()
    {
        // Act
        var token = _jwtTokenService.CreateToken("testuser", "Admin");

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.Contains("TestAudience", jwtToken.Audiences);
    }

    [Fact]
    public void CreateToken_WithUsernameAndRole_HasCorrectExpiration()
    {
        // Act
        var beforeCreation = DateTime.UtcNow;
        var token = _jwtTokenService.CreateToken("testuser", "Admin");
        var afterCreation = DateTime.UtcNow;

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // The token should expire approximately 60 minutes from now
        var expectedExpiration = beforeCreation.AddMinutes(60);
        Assert.True(jwtToken.ValidTo >= expectedExpiration.AddSeconds(-5));
        Assert.True(jwtToken.ValidTo <= afterCreation.AddMinutes(60).AddSeconds(5));
    }

    // --- CreateToken (with permissions) Tests ---

    [Fact]
    public void CreateToken_WithPermissions_ReturnsValidJwt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roles = new[] { "Admin", "Operator" };
        var permissions = new[]
        {
            new PermissionInfo { Resource = "vm", Action = "read" },
            new PermissionInfo { Resource = "vm", Action = "create" }
        };

        // Act
        var token = _jwtTokenService.CreateToken(userId, "testuser", roles, permissions);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public void CreateToken_WithPermissions_ContainsUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roles = new[] { "Admin" };
        var permissions = Array.Empty<PermissionInfo>();

        // Act
        var token = _jwtTokenService.CreateToken(userId, "testuser", roles, permissions);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.Contains(jwtToken.Claims, c => c.Type == "userId" && c.Value == userId.ToString());
    }

    [Fact]
    public void CreateToken_WithPermissions_ContainsUsername()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roles = new[] { "Admin" };
        var permissions = Array.Empty<PermissionInfo>();

        // Act
        var token = _jwtTokenService.CreateToken(userId, "testuser", roles, permissions);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.Contains(jwtToken.Claims, c => c.Type == ClaimTypes.Name && c.Value == "testuser");
    }

    [Fact]
    public void CreateToken_WithPermissions_ContainsAllRoles()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roles = new[] { "Admin", "Operator" };
        var permissions = Array.Empty<PermissionInfo>();

        // Act
        var token = _jwtTokenService.CreateToken(userId, "testuser", roles, permissions);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var roleClaims = jwtToken.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Contains("Admin", roleClaims);
        Assert.Contains("Operator", roleClaims);
    }

    [Fact]
    public void CreateToken_WithPermissions_ContainsPermissionClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roles = new[] { "Admin" };
        var permissions = new[]
        {
            new PermissionInfo { Resource = "vm", Action = "read" },
            new PermissionInfo { Resource = "host", Action = "update" }
        };

        // Act
        var token = _jwtTokenService.CreateToken(userId, "testuser", roles, permissions);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var permissionClaims = jwtToken.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();
        Assert.Contains("vm:read", permissionClaims);
        Assert.Contains("host:update", permissionClaims);
    }

    [Fact]
    public void CreateToken_WithEmptyPermissions_DoesNotContainPermissionClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roles = new[] { "Admin" };
        var permissions = Array.Empty<PermissionInfo>();

        // Act
        var token = _jwtTokenService.CreateToken(userId, "testuser", roles, permissions);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var permissionClaims = jwtToken.Claims.Where(c => c.Type == "permission").ToList();
        Assert.Empty(permissionClaims);
    }

    [Fact]
    public void CreateToken_DifferentUsers_ProduceDifferentTokens()
    {
        // Act
        var token1 = _jwtTokenService.CreateToken("user1", "Admin");
        var token2 = _jwtTokenService.CreateToken("user2", "ReadOnly");

        // Assert
        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void CreateToken_TokenExpirationMatchesConfiguration()
    {
        // Arrange
        var shortExpirationOptions = new JwtOptions
        {
            Secret = "ThisIsAVeryLongSecretKeyForTestingPurposesOnly1234567890!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationMinutes = 15
        };
        var shortExpirationService = new JwtTokenService(Options.Create(shortExpirationOptions));

        // Act
        var beforeCreation = DateTime.UtcNow;
        var token = shortExpirationService.CreateToken("testuser", "Admin");

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var expectedExpiration = beforeCreation.AddMinutes(15);
        // Allow 10-second tolerance
        Assert.True(jwtToken.ValidTo >= expectedExpiration.AddSeconds(-10));
        Assert.True(jwtToken.ValidTo <= expectedExpiration.AddSeconds(10));
    }
}
