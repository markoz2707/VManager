using Xunit;
using Microsoft.AspNetCore.Mvc;
using HyperV.Agent.Controllers;
using HyperV.Contracts.Services;
using Moq;
using FluentAssertions;
using System.Text.Json;

namespace HyperV.Tests.Controllers;

public class ReplicationControllerTests : IDisposable
{
    private readonly ReplicationController _controller;
    private readonly Mock<IReplicationService> _mockReplicationService;

    public ReplicationControllerTests()
    {
        _mockReplicationService = new Mock<IReplicationService>();
        _controller = new ReplicationController(_mockReplicationService.Object);
    }

    [Fact]
    public void TestCreateReplicationRelationship_Success()
    {
        // Arrange
        var request = new CreateReplicationRequest { SourceVm = "TestVM", TargetHost = "TargetHost", AuthMode = "Certificate" };
        var expectedResult = "{\"relationshipId\":\"rel-123\",\"status\":\"Completed\"}";
        _mockReplicationService.Setup(x => x.CreateReplicationRelationship(request.SourceVm, request.TargetHost, request.AuthMode)).Returns(expectedResult);

        // Act
        var result = _controller.CreateReplicationRelationship(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var json = JsonSerializer.Serialize(okResult.Value);
        json.Should().Contain("rel-123");
        _mockReplicationService.Verify(x => x.CreateReplicationRelationship(request.SourceVm, request.TargetHost, request.AuthMode), Times.Once);
    }

    [Fact]
    public void TestCreateReplicationRelationship_BadRequest()
    {
        // Arrange
        var request = new CreateReplicationRequest { SourceVm = "TestVM", TargetHost = "TargetHost" };
        _mockReplicationService.Setup(x => x.CreateReplicationRelationship(request.SourceVm, request.TargetHost, request.AuthMode))
            .Throws(new InvalidOperationException("Invalid parameters"));

        // Act
        var result = _controller.CreateReplicationRelationship(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void TestInitiateFailover_Accepted()
    {
        // Arrange
        var vmName = "TestVM";
        var request = new FailoverRequest { Mode = "Planned" };
        var expectedResult = "{\"jobId\":\"failover-job-456\",\"status\":\"Completed\"}";
        _mockReplicationService.Setup(x => x.InitiateFailover(vmName, request.Mode)).Returns(expectedResult);

        // Act
        var result = _controller.InitiateFailover(vmName, request);

        // Assert
        result.Should().BeOfType<AcceptedResult>();
        var acceptedResult = (AcceptedResult)result;
        var json = JsonSerializer.Serialize(acceptedResult.Value);
        json.Should().Contain("failover-job-456");
        _mockReplicationService.Verify(x => x.InitiateFailover(vmName, request.Mode), Times.Once);
    }

    [Fact]
    public void TestInitiateFailover_BadRequest()
    {
        // Arrange
        var vmName = "TestVM";
        var request = new FailoverRequest { Mode = "Planned" };
        _mockReplicationService.Setup(x => x.InitiateFailover(vmName, request.Mode))
            .Throws(new InvalidOperationException("Failover failed"));

        // Act
        var result = _controller.InitiateFailover(vmName, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void TestGetReplicationState_OK()
    {
        // Arrange
        var vmName = "TestVM";
        var expectedState = "{\"vmName\":\"test-vm\",\"enabledState\":\"Enabled\",\"replicationHealth\":\"OK\"}";
        _mockReplicationService.Setup(x => x.GetReplicationState(vmName)).Returns(expectedState);

        // Act
        var result = _controller.GetReplicationState(vmName);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var json = JsonSerializer.Serialize(okResult.Value);
        json.Should().Contain("OK");
        _mockReplicationService.Verify(x => x.GetReplicationState(vmName), Times.Once);
    }

    [Fact]
    public void TestGetReplicationState_BadRequest()
    {
        // Arrange
        var vmName = "TestVM";
        _mockReplicationService.Setup(x => x.GetReplicationState(vmName))
            .Throws(new InvalidOperationException("Relationship not found"));

        // Act
        var result = _controller.GetReplicationState(vmName);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void TestStartReplication_OK()
    {
        // Arrange
        var vmName = "TestVM";
        _mockReplicationService.Setup(x => x.StartReplication(vmName));

        // Act
        var result = _controller.StartReplication(vmName);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockReplicationService.Verify(x => x.StartReplication(vmName), Times.Once);
    }

    [Fact]
    public void TestStartReplication_BadRequest()
    {
        // Arrange
        var vmName = "TestVM";
        _mockReplicationService.Setup(x => x.StartReplication(vmName))
            .Throws(new InvalidOperationException("Start failed"));

        // Act
        var result = _controller.StartReplication(vmName);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void TestReverseReplicationRelationship_OK()
    {
        // Arrange
        var vmName = "TestVM";
        _mockReplicationService.Setup(x => x.ReverseReplicationRelationship(vmName));

        // Act
        var result = _controller.ReverseReplicationRelationship(vmName);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockReplicationService.Verify(x => x.ReverseReplicationRelationship(vmName), Times.Once);
    }

    [Fact]
    public void TestReverseReplicationRelationship_BadRequest()
    {
        // Arrange
        var vmName = "TestVM";
        _mockReplicationService.Setup(x => x.ReverseReplicationRelationship(vmName))
            .Throws(new InvalidOperationException("Reverse failed"));

        // Act
        var result = _controller.ReverseReplicationRelationship(vmName);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void TestAddAuthorizationEntry_OK()
    {
        // Arrange
        var relationshipId = "rel-123";
        var entry = "{\"user\":\"test\",\"permission\":\"read\"}";
        _mockReplicationService.Setup(x => x.AddAuthorizationEntry(relationshipId, entry));

        // Act
        var result = _controller.AddAuthorizationEntry(relationshipId, entry);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockReplicationService.Verify(x => x.AddAuthorizationEntry(relationshipId, entry), Times.Once);
    }

    [Fact]
    public void TestAddAuthorizationEntry_BadRequest()
    {
        // Arrange
        var relationshipId = "rel-123";
        var entry = "{\"user\":\"test\",\"permission\":\"read\"}";
        _mockReplicationService.Setup(x => x.AddAuthorizationEntry(relationshipId, entry))
            .Throws(new InvalidOperationException("Add failed"));

        // Act
        var result = _controller.AddAuthorizationEntry(relationshipId, entry);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}