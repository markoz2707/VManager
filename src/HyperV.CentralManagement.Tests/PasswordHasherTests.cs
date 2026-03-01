using HyperV.CentralManagement.Services;
using Xunit;

namespace HyperV.CentralManagement.Tests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _passwordHasher;

    public PasswordHasherTests()
    {
        _passwordHasher = new PasswordHasher();
    }

    // --- Hash Tests ---

    [Fact]
    public void Hash_WithValidPassword_ReturnsNonNullHash()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash = _passwordHasher.Hash(password);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void Hash_WithValidPassword_ReturnsCorrectFormat()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash = _passwordHasher.Hash(password);

        // Assert
        // Format: iterations.salt.hash (3 parts separated by dots)
        var parts = hash.Split('.', 3);
        Assert.Equal(3, parts.Length);
        Assert.True(int.TryParse(parts[0], out int iterations));
        Assert.Equal(100_000, iterations);
    }

    [Fact]
    public void Hash_DifferentPasswords_ProduceDifferentHashes()
    {
        // Arrange
        var password1 = "Password1!";
        var password2 = "Password2!";

        // Act
        var hash1 = _passwordHasher.Hash(password1);
        var hash2 = _passwordHasher.Hash(password2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Hash_SamePassword_ProducesDifferentHashes()
    {
        // Arrange (different salt each time)
        var password = "TestPassword123!";

        // Act
        var hash1 = _passwordHasher.Hash(password);
        var hash2 = _passwordHasher.Hash(password);

        // Assert
        // Due to random salt, same password should produce different hashes
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Hash_EmptyPassword_ReturnsNonNullHash()
    {
        // Arrange
        var password = "";

        // Act
        var hash = _passwordHasher.Hash(password);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void Hash_LongPassword_ReturnsNonNullHash()
    {
        // Arrange
        var password = new string('A', 1000);

        // Act
        var hash = _passwordHasher.Hash(password);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    // --- Verify Tests ---

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _passwordHasher.Hash(password);

        // Act
        var result = _passwordHasher.Verify(password, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _passwordHasher.Hash(password);

        // Act
        var result = _passwordHasher.Verify("WrongPassword!", hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_WithEmptyPassword_AgainstNonEmptyHash_ReturnsFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _passwordHasher.Hash(password);

        // Act
        var result = _passwordHasher.Verify("", hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_WithCorrectEmptyPassword_ReturnsTrue()
    {
        // Arrange
        var password = "";
        var hash = _passwordHasher.Hash(password);

        // Act
        var result = _passwordHasher.Verify(password, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Verify_WithMalformedHash_ReturnsFalse()
    {
        // Arrange
        var malformedHash = "not-a-valid-hash";

        // Act
        var result = _passwordHasher.Verify("password", malformedHash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_WithInvalidIterationsInHash_ReturnsFalse()
    {
        // Arrange
        var invalidHash = "notanumber.c2FsdA==.aGFzaA==";

        // Act
        var result = _passwordHasher.Verify("password", invalidHash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_WithSimilarPassword_ReturnsFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _passwordHasher.Hash(password);

        // Act
        var result = _passwordHasher.Verify("testpassword123!", hash); // lowercase

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_WithUnicodePassword_ReturnsTrue()
    {
        // Arrange
        var password = "P@ssw0rd\u00E9\u00FC\u00F1";
        var hash = _passwordHasher.Hash(password);

        // Act
        var result = _passwordHasher.Verify(password, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Verify_WithLongPassword_ReturnsTrue()
    {
        // Arrange
        var password = new string('A', 1000);
        var hash = _passwordHasher.Hash(password);

        // Act
        var result = _passwordHasher.Verify(password, hash);

        // Assert
        Assert.True(result);
    }

    // --- Round-trip Tests ---

    [Fact]
    public void HashAndVerify_RoundTrip_SucceedsForMultiplePasswords()
    {
        // Arrange
        var passwords = new[]
        {
            "simple",
            "Complex!P@ssw0rd",
            "12345678",
            "a",
            "SpecialChars!@#$%^&*()",
            "    spaces    "
        };

        // Act & Assert
        foreach (var password in passwords)
        {
            var hash = _passwordHasher.Hash(password);
            Assert.True(_passwordHasher.Verify(password, hash), $"Failed for password: '{password}'");
        }
    }
}
