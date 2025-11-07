using Xunit;
using Microsoft.AspNetCore.Mvc;
using HyperV.Agent.Controllers;
using HyperV.Core.Hcn.Services;
using HyperV.HyperV.Contracts.Services;
using HyperV.Tests.Helpers;
using Moq;
using FluentAssertions;
using System.ComponentModel;
using System.Text.Json;

namespace HyperV.Tests.Controllers;

public class NetworksControllerTests : IDisposable
{
    private readonly NetworksController _controller;
    private readonly Mock<NetworkService> _mockNetworkService;
    private readonly Mock<IFibreChannelService> _mockFibreChannelService;
    private readonly Mock<WmiNetworkService> _mockWmiNetworkService;

    public NetworksControllerTests()
    {
        _mockNetworkService = ServiceMocks.ConfigureNetworkServiceMock();
        _mockFibreChannelService = new Mock<IFibreChannelService>();
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<NetworksController>>();
        _mockWmiNetworkService = new Mock<WmiNetworkService>();
        _controller = new NetworksController(_mockNetworkService.Object, _mockWmiNetworkService.Object, mockLogger.Object, _mockFibreChannelService.Object);
    }

    #region Existing Tests - Network Operations

    [Fact]
    public void CreateNat_WithValidParameters_ShouldReturnOkResult()
    {
        // Arrange
        var networkName = "TestNetwork";
        var prefix = "192.168.100.0/24";
        var expectedId = Guid.NewGuid();
        _mockNetworkService.Setup(x => x.CreateNATNetwork(networkName, prefix))
            .Returns(expectedId);

        // Act
        var result = _controller.CreateNat(networkName, prefix);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("id").GetGuid().Should().Be(expectedId);
        
        _mockNetworkService.Verify(x => x.CreateNATNetwork(networkName, prefix), Times.Once);
    }

    [Fact]
    public void CreateNat_WithDefaultPrefix_ShouldReturnOkResult()
    {
        // Arrange
        var networkName = "TestNetwork";
        var defaultPrefix = "192.168.100.0/24";
        var expectedId = Guid.NewGuid();
        _mockNetworkService.Setup(x => x.CreateNATNetwork(networkName, defaultPrefix))
            .Returns(expectedId);

        // Act
        var result = _controller.CreateNat(networkName);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockNetworkService.Verify(x => x.CreateNATNetwork(networkName, defaultPrefix), Times.Once);
    }

    [Fact]
    public void CreateNat_WhenServiceThrows_ShouldRethrowException()
    {
        // Arrange
        var networkName = "TestNetwork";
        var prefix = "192.168.100.0/24";
        _mockNetworkService.Setup(x => x.CreateNATNetwork(networkName, prefix))
            .Throws(new Exception("Network creation failed"));

        // Act & Assert
        var exception = Assert.Throws<Exception>(() => _controller.CreateNat(networkName, prefix));
        exception.Message.Should().Be("Network creation failed");
    }

    [Fact]
    public void DeleteNetwork_WithValidId_ShouldReturnOkResult()
    {
        // Arrange
        var networkId = Guid.NewGuid();
        _mockNetworkService.Setup(x => x.DeleteNetwork(networkId))
            .Verifiable();

        // Act
        var result = _controller.DeleteNetwork(networkId);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockNetworkService.Verify(x => x.DeleteNetwork(networkId), Times.Once);
    }

    [Fact]
    public void DeleteNetwork_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var networkId = Guid.NewGuid();
        _mockNetworkService.Setup(x => x.DeleteNetwork(networkId))
            .Throws(new Win32Exception(2)); // ERROR_FILE_NOT_FOUND

        // Act
        var result = _controller.DeleteNetwork(networkId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void DeleteNetwork_WhenServiceThrowsOtherException_ShouldRethrowException()
    {
        // Arrange
        var networkId = Guid.NewGuid();
        _mockNetworkService.Setup(x => x.DeleteNetwork(networkId))
            .Throws(new Exception("Delete failed"));

        // Act & Assert
        var exception = Assert.Throws<Exception>(() => _controller.DeleteNetwork(networkId));
        exception.Message.Should().Be("Delete failed");
    }

    [Fact]
    public void GetNetworkProperties_WithValidId_ShouldReturnOkResult()
    {
        // Arrange
        var networkId = Guid.NewGuid();
        var query = "basic";
        var mockProperties = JsonSerializer.Serialize(new
        {
            Id = networkId,
            Name = "TestNetwork",
            Type = "NAT",
            Subnet = "192.168.100.0/24"
        });
        
        _mockNetworkService.Setup(x => x.QueryNetworkProperties(networkId, query))
            .Returns(mockProperties);

        // Act
        var result = _controller.GetNetworkProperties(networkId, query);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().Be(mockProperties);
        
        _mockNetworkService.Verify(x => x.QueryNetworkProperties(networkId, query), Times.Once);
    }

    [Fact]
    public void GetNetworkProperties_WithEmptyQuery_ShouldReturnOkResult()
    {
        // Arrange
        var networkId = Guid.NewGuid();
        var emptyQuery = "";
        var mockProperties = JsonSerializer.Serialize(new { Id = networkId });
        
        _mockNetworkService.Setup(x => x.QueryNetworkProperties(networkId, emptyQuery))
            .Returns(mockProperties);

        // Act
        var result = _controller.GetNetworkProperties(networkId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockNetworkService.Verify(x => x.QueryNetworkProperties(networkId, emptyQuery), Times.Once);
    }

    [Fact]
    public void GetNetworkProperties_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var networkId = Guid.NewGuid();
        _mockNetworkService.Setup(x => x.QueryNetworkProperties(networkId, It.IsAny<string>()))
            .Throws(new Win32Exception(2)); // ERROR_FILE_NOT_FOUND

        // Act
        var result = _controller.GetNetworkProperties(networkId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void CreateEndpoint_WithValidParameters_ShouldReturnOkResult()
    {
        // Arrange
        var networkId = Guid.NewGuid();
        var endpointName = "TestEndpoint";
        var ipAddress = "192.168.100.10";
        var expectedEndpointId = Guid.NewGuid();
        
        _mockNetworkService.Setup(x => x.CreateEndpoint(networkId, endpointName, ipAddress))
            .Returns(expectedEndpointId);

        // Act
        var result = _controller.CreateEndpoint(networkId, endpointName, ipAddress);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("id").GetGuid().Should().Be(expectedEndpointId);
        
        _mockNetworkService.Verify(x => x.CreateEndpoint(networkId, endpointName, ipAddress), Times.Once);
    }

    [Fact]
    public void CreateEndpoint_WithEmptyIpAddress_ShouldReturnOkResult()
    {
        // Arrange
        var networkId = Guid.NewGuid();
        var endpointName = "TestEndpoint";
        var emptyIpAddress = "";
        var expectedEndpointId = Guid.NewGuid();
        
        _mockNetworkService.Setup(x => x.CreateEndpoint(networkId, endpointName, emptyIpAddress))
            .Returns(expectedEndpointId);

        // Act
        var result = _controller.CreateEndpoint(networkId, endpointName);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockNetworkService.Verify(x => x.CreateEndpoint(networkId, endpointName, emptyIpAddress), Times.Once);
    }

    [Fact]
    public void CreateEndpoint_WithNonExistentNetwork_ShouldReturnNotFound()
    {
        // Arrange
        var networkId = Guid.NewGuid();
        var endpointName = "TestEndpoint";
        _mockNetworkService.Setup(x => x.CreateEndpoint(networkId, endpointName, It.IsAny<string>()))
            .Throws(new Win32Exception(2)); // ERROR_FILE_NOT_FOUND

        // Act
        var result = _controller.CreateEndpoint(networkId, endpointName);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void DeleteEndpoint_WithValidId_ShouldReturnOkResult()
    {
        // Arrange
        var endpointId = Guid.NewGuid();
        _mockNetworkService.Setup(x => x.DeleteEndpoint(endpointId))
            .Verifiable();

        // Act
        var result = _controller.DeleteEndpoint(endpointId);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockNetworkService.Verify(x => x.DeleteEndpoint(endpointId), Times.Once);
    }

    [Fact]
    public void DeleteEndpoint_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var endpointId = Guid.NewGuid();
        _mockNetworkService.Setup(x => x.DeleteEndpoint(endpointId))
            .Throws(new Win32Exception(2)); // ERROR_FILE_NOT_FOUND

        // Act
        var result = _controller.DeleteEndpoint(endpointId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetEndpointProperties_WithValidId_ShouldReturnOkResult()
    {
        // Arrange
        var endpointId = Guid.NewGuid();
        var query = "detailed";
        var mockProperties = JsonSerializer.Serialize(new
        {
            Id = endpointId,
            Name = "TestEndpoint",
            IpAddress = "192.168.100.10",
            MacAddress = "00:15:5D:00:00:01"
        });
        
        _mockNetworkService.Setup(x => x.QueryEndpointProperties(endpointId, query))
            .Returns(mockProperties);

        // Act
        var result = _controller.GetEndpointProperties(endpointId, query);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().Be(mockProperties);
        
        _mockNetworkService.Verify(x => x.QueryEndpointProperties(endpointId, query), Times.Once);
    }

    [Fact]
    public void GetEndpointProperties_WithEmptyQuery_ShouldReturnOkResult()
    {
        // Arrange
        var endpointId = Guid.NewGuid();
        var emptyQuery = "";
        var mockProperties = JsonSerializer.Serialize(new { Id = endpointId });
        
        _mockNetworkService.Setup(x => x.QueryEndpointProperties(endpointId, emptyQuery))
            .Returns(mockProperties);

        // Act
        var result = _controller.GetEndpointProperties(endpointId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockNetworkService.Verify(x => x.QueryEndpointProperties(endpointId, emptyQuery), Times.Once);
    }

    [Fact]
    public void GetEndpointProperties_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var endpointId = Guid.NewGuid();
        _mockNetworkService.Setup(x => x.QueryEndpointProperties(endpointId, It.IsAny<string>()))
            .Throws(new Win32Exception(2)); // ERROR_FILE_NOT_FOUND

        // Act
        var result = _controller.GetEndpointProperties(endpointId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region FibreChannel Extension Tests

    [Fact]
    public void TestCreateSan_Success()
    {
        // Arrange
        var sanName = "TestSAN";
        var request = new { Name = sanName, Wwnn = "50:00:00:00:00:00:00:01", Wwpn = "50:00:00:00:00:00:00:02" };
        var expectedResult = "{\"sanId\":\"san-123\",\"status\":\"Created\"}";
        _mockFibreChannelService.Setup(x => x.CreateSan(It.IsAny<string>(), It.IsAny<string>())).Returns(expectedResult);

        // Act
        var result = _controller.CreateSan(request.Name, request.Wwnn, request.Wwpn);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var json = JsonSerializer.Serialize(okResult.Value);
        json.Should().Contain("san-123");
        _mockFibreChannelService.Verify(x => x.CreateSan(sanName, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void TestCreateSan_BadRequest()
    {
        // Arrange
        var sanName = "TestSAN";
        var request = new { Name = sanName, Wwnn = "50:00:00:00:00:00:00:01", Wwpn = "50:00:00:00:00:00:00:02" };
        _mockFibreChannelService.Setup(x => x.CreateSan(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("SAN creation failed"));

        // Act
        var result = _controller.CreateSan(request.Name, request.Wwnn, request.Wwpn);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void TestDeleteSan_OK()
    {
        // Arrange
        var sanId = "san-123";
        _mockFibreChannelService.Setup(x => x.DeleteSan(sanId));

        // Act
        var result = _controller.DeleteSan(sanId);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockFibreChannelService.Verify(x => x.DeleteSan(sanId), Times.Once);
    }

    [Fact]
    public void TestDeleteSan_NotFound()
    {
        // Arrange
        var sanId = "san-123";
        _mockFibreChannelService.Setup(x => x.DeleteSan(sanId))
            .Throws(new InvalidOperationException("SAN not found"));

        // Act
        var result = _controller.DeleteSan(sanId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void TestGetSanInfo_OK()
    {
        // Arrange
        var sanId = "san-123";
        var expectedInfo = "{\"id\":\"san-123\",\"name\":\"TestSAN\",\"status\":\"OK\"}";
        _mockFibreChannelService.Setup(x => x.GetSanInfo(sanId)).Returns(expectedInfo);

        // Act
        var result = _controller.GetSanInfo(sanId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var json = JsonSerializer.Serialize(okResult.Value);
        json.Should().Contain("OK");
        _mockFibreChannelService.Verify(x => x.GetSanInfo(sanId), Times.Once);
    }

    [Fact]
    public void TestGetSanInfo_NotFound()
    {
        // Arrange
        var sanId = "san-123";
        _mockFibreChannelService.Setup(x => x.GetSanInfo(sanId))
            .Throws(new InvalidOperationException("SAN not found"));

        // Act
        var result = _controller.GetSanInfo(sanId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void TestCreateVirtualFcPort_OK()
    {
        // Arrange
        var sanId = "san-123";
        var portName = "TestPort";
        var expectedPortId = "port-456";
        _mockFibreChannelService.Setup(x => x.CreateVirtualFcPort(sanId, portName)).Returns(expectedPortId);

        // Act
        var result = _controller.CreateVirtualFcPort(sanId, portName);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(new { portId = "port-456" });
        _mockFibreChannelService.Verify(x => x.CreateVirtualFcPort(sanId, portName), Times.Once);
    }

    [Fact]
    public void TestCreateVirtualFcPort_BadRequest()
    {
        // Arrange
        var sanId = "san-123";
        var portName = "TestPort";
        _mockFibreChannelService.Setup(x => x.CreateVirtualFcPort(sanId, portName))
            .Throws(new InvalidOperationException("Port creation failed"));

        // Act
        var result = _controller.CreateVirtualFcPort(sanId, portName);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    public void Dispose()
    {
        // Cleanup if needed
    }
}