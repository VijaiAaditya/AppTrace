using AppTrace.Collector.Services;
using AppTrace.Common.Models;
using AppTrace.Storage;
using AutoFixture;
using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenTelemetry.Proto.Collector.Logs.V1;
using Xunit;
using Google.Protobuf;
using NSubstitute.ExceptionExtensions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace AppTrace.Collector.Tests;

/// <summary>
/// Unit tests for OtlpLogsService using modern testing patterns
/// </summary>
public class OtlpLogsServiceTests
{
    private readonly IFixture _fixture;
    private readonly ILogger<OtlpLogsService> _mockLogger;
    private readonly ILogStorage _mockLogStorage;
    private readonly OtlpLogsService _sut;

    public OtlpLogsServiceTests()
    {
        _fixture = new Fixture();
        _mockLogger = Substitute.For<ILogger<OtlpLogsService>>();
        _mockLogStorage = Substitute.For<ILogStorage>();
        _sut = new OtlpLogsService(_mockLogger, _mockLogStorage);
    }

    [Fact]
    public async Task Export_WithValidRequest_ShouldProcessLogsSuccessfully()
    {
        // Arrange
        var request = CreateValidLogRequest();
        var context = Substitute.For<ServerCallContext>();
        var capturedLogs = new List<LogEntry>();

        _mockLogStorage
            .When(x => x.InsertLogsAsync(Arg.Any<IEnumerable<LogEntry>>()))
            .Do(callInfo => capturedLogs.AddRange(callInfo.Arg<IEnumerable<LogEntry>>()));

        // Act
        var response = await _sut.Export(request, context);

        // Assert
        response.Should().NotBeNull();
        response.PartialSuccess.RejectedLogRecords.Should().Be(0);
        response.PartialSuccess.ErrorMessage.Should().BeEmpty();

        await _mockLogStorage.Received(1).InsertLogsAsync(Arg.Any<IEnumerable<LogEntry>>());
        
        capturedLogs.Should().HaveCount(2);
        capturedLogs.Should().AllSatisfy(log =>
        {
            log.Id.Should().NotBeEmpty();
            log.Timestamp.Should().BeAfter(DateTimeOffset.MinValue);
            log.Severity.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task Export_WhenStorageThrows_ShouldReturnPartialFailure()
    {
        // Arrange
        var request = CreateValidLogRequest();
        var context = Substitute.For<ServerCallContext>();
        var expectedException = new InvalidOperationException("Database connection failed");

        _mockLogStorage
            .InsertLogsAsync(Arg.Any<IEnumerable<LogEntry>>())
            .Throws(expectedException);

        // Act
        var response = await _sut.Export(request, context);

        // Assert
        response.Should().NotBeNull();
        response.PartialSuccess.RejectedLogRecords.Should().Be(2);
        response.PartialSuccess.ErrorMessage.Should().Contain("Database connection failed");
    }

    [Fact]
    public async Task Export_WithEmptyRequest_ShouldHandleGracefully()
    {
        // Arrange
        var request = new ExportLogsServiceRequest();
        var context = Substitute.For<ServerCallContext>();

        // Act
        var response = await _sut.Export(request, context);

        // Assert
        response.Should().NotBeNull();
        response.PartialSuccess.RejectedLogRecords.Should().Be(0);

        await _mockLogStorage.DidNotReceive().InsertLogsAsync(Arg.Any<IEnumerable<LogEntry>>());
    }

    [Theory]
    [InlineData("ERROR")]
    [InlineData("WARN")]
    [InlineData("INFO")]
    [InlineData("DEBUG")]
    public async Task Export_WithDifferentSeverities_ShouldPreserveSeverity(string severity)
    {
        // Arrange
        var request = CreateLogRequestWithSeverity(severity);
        var context = Substitute.For<ServerCallContext>();
        var capturedLogs = new List<LogEntry>();

        _mockLogStorage
            .When(x => x.InsertLogsAsync(Arg.Any<IEnumerable<LogEntry>>()))
            .Do(callInfo => capturedLogs.AddRange(callInfo.Arg<IEnumerable<LogEntry>>()));

        // Act
        await _sut.Export(request, context);

        // Assert
        capturedLogs.Should().AllSatisfy(log => 
            log.Severity.Should().Be(severity));
    }

    private static ExportLogsServiceRequest CreateValidLogRequest()
    {
        var request = new ExportLogsServiceRequest();
        var resourceLog = new ResourceLogs
        {
            Resource = new Resource()
        };
        
        resourceLog.Resource.Attributes.Add(new KeyValue
        {
            Key = "service.name",
            Value = new AnyValue { StringValue = "test-service" }
        });

        var scopeLog = new ScopeLogs();
        scopeLog.LogRecords.Add(new LogRecord
        {
            TimeUnixNano = GetUnixTimeNanoseconds(DateTimeOffset.UtcNow),
            SeverityText = "INFO",
            Body = new AnyValue { StringValue = "Test log message 1" }
        });
        
        scopeLog.LogRecords.Add(new LogRecord
        {
            TimeUnixNano = GetUnixTimeNanoseconds(DateTimeOffset.UtcNow),
            SeverityText = "ERROR", 
            Body = new AnyValue { StringValue = "Test log message 2" }
        });

        resourceLog.ScopeLogs.Add(scopeLog);
        request.ResourceLogs.Add(resourceLog);

        return request;
    }

    private static ExportLogsServiceRequest CreateLogRequestWithSeverity(string severity)
    {
        var request = new ExportLogsServiceRequest();
        var resourceLog = new ResourceLogs
        {
            Resource = new Resource()
        };
        
        resourceLog.Resource.Attributes.Add(new KeyValue
        {
            Key = "service.name",
            Value = new AnyValue { StringValue = "test-service" }
        });

        var scopeLog = new ScopeLogs();
        scopeLog.LogRecords.Add(new LogRecord
        {
            TimeUnixNano = GetUnixTimeNanoseconds(DateTimeOffset.UtcNow),
            SeverityText = severity,
            Body = new AnyValue { StringValue = $"Test {severity} message" }
        });

        resourceLog.ScopeLogs.Add(scopeLog);
        request.ResourceLogs.Add(resourceLog);

        return request;
    }
    private static ulong GetUnixTimeNanoseconds(DateTimeOffset dateTimeOffset)
    {
        return (ulong)(dateTimeOffset.ToUnixTimeMilliseconds() * 1_000_000);
    }
}