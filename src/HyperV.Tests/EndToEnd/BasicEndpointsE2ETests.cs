using Xunit;
using System.Net;
using FluentAssertions;
using System.Text.Json;

namespace HyperV.Tests.EndToEnd;

/// <summary>
/// Testy end-to-end dla podstawowych endpointów API
/// UWAGA: Te testy wymagają uruchomionego serwera API na porcie 8743
/// </summary>
[Collection("EndToEnd")]
public class BasicEndpointsE2ETests : EndToEndTestBase
{
    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthyStatus()
    {
        // Act & Assert
        await VerifyJsonEndpoint("/api/v1/service/health");
        
        var response = await GetAsync("/api/v1/service/health");
        using var json = await GetJsonResponseAsync(response);
        var root = json.RootElement;
        
        root.GetProperty("status").GetString().Should().Be("healthy");
        root.GetProperty("version").GetString().Should().Be("1.0.0");
        root.TryGetProperty("services", out _).Should().BeTrue();
    }

    [Fact]
    public async Task InfoEndpoint_ShouldReturnAgentInfo()
    {
        // Act & Assert
        await VerifyJsonEndpoint("/api/v1/service/info");
        
        var response = await GetAsync("/api/v1/service/info");
        using var json = await GetJsonResponseAsync(response);
        var root = json.RootElement;
        
        root.GetProperty("name").GetString().Should().Be("HyperV Agent");
        root.GetProperty("version").GetString().Should().Be("1.0.0");
        root.TryGetProperty("endpoints", out _).Should().BeTrue();
        root.TryGetProperty("capabilities", out _).Should().BeTrue();
    }

    [Fact]
    public async Task VmsListEndpoint_ShouldReturnVmsList()
    {
        // Act
        var response = await GetAsync("/api/v1/vms");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        using var json = await GetJsonResponseAsync(response);
        var root = json.RootElement;
        
        root.TryGetProperty("HCS", out _).Should().BeTrue();
        root.TryGetProperty("WMI", out _).Should().BeTrue();
    }

    [Fact]
    public async Task VmPresent_WithNonExistentVm_ShouldReturnNotPresent()
    {
        // Arrange
        var vmName = $"non-existent-vm-{Guid.NewGuid():N}";
        
        // Act
        var response = await GetAsync($"/api/v1/vms/{vmName}/present");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        using var json = await GetJsonResponseAsync(response);
        var root = json.RootElement;
        
        root.GetProperty("present").GetBoolean().Should().BeFalse();
        root.GetProperty("hcs").GetBoolean().Should().BeFalse();
        root.GetProperty("wmi").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ContainersList_ShouldReturnContainersList()
    {
        // Act
        var response = await GetAsync("/api/v1/containers");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        using var json = await GetJsonResponseAsync(response);
        var root = json.RootElement;
        
        root.TryGetProperty("HcsContainers", out _).Should().BeTrue();
        root.TryGetProperty("WmiContainers", out _).Should().BeTrue();
    }

    [Fact]
    public async Task StorageJobsList_ShouldReturnJobsList()
    {
        // Act
        var response = await GetAsync("/api/v1/jobs/storage");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        
        // Should return array (empty or with jobs)
        using var json = JsonDocument.Parse(content);
        json.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task NonExistentEndpoint_ShouldReturn404()
    {
        // Act
        var response = await GetAsync("/api/v1/non-existent-endpoint");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetVmProperties_WithNonExistentVm_ShouldReturn404()
    {
        // Arrange
        var vmName = $"non-existent-vm-{Guid.NewGuid():N}";
        
        // Act
        var response = await GetAsync($"/api/v1/vms/{vmName}/properties");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StartVm_WithNonExistentVm_ShouldReturn404()
    {
        // Arrange
        var vmName = $"non-existent-vm-{Guid.NewGuid():N}";
        
        // Act
        var response = await Client.PostAsync($"/api/v1/vms/{vmName}/start", null);
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateVm_WithEmptyBody_ShouldReturn400()
    {
        // Act
        var response = await PostAsync("/api/v1/vms", new { });
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Test kompleksowego scenariusza - sprawdzenie wszystkich podstawowych endpointów
    /// </summary>
    [Fact]
    public async Task CompleteApiCheck_ShouldVerifyAllBasicEndpoints()
    {
        // Test health endpoint
        await VerifyJsonEndpoint("/api/v1/service/health");
        
        // Test info endpoint
        await VerifyJsonEndpoint("/api/v1/service/info");
        
        // Test VMs list endpoint
        await VerifyJsonEndpoint("/api/v1/vms");
        
        // Test containers list endpoint
        await VerifyJsonEndpoint("/api/v1/containers");
        
        // Test storage jobs endpoint
        await VerifyJsonEndpoint("/api/v1/jobs/storage");
        
        // Test that all basic endpoints are accessible and return valid JSON
        Assert.True(true, "All basic API endpoints are accessible and return valid responses");
    }

    protected override async Task CleanupTestDataAsync()
    {
        // Cleanup any test data created during E2E tests
        // This is where we would clean up any VMs, containers, etc. created during testing
        await Task.CompletedTask;
    }
}