using AppTrace.Common.Models;
using AppTrace.Storage;
using AutoFixture;
using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AppTrace.Storage.Tests;

/// <summary>
/// Unit tests for InMemoryLogStorage using modern testing patterns
/// </summary>
public class InMemoryLogStorageTests
{
    private readonly IFixture _fixture;
    private readonly InMemoryLogStorage _sut; // System Under Test

    public InMemoryLogStorageTests()
    {
        _fixture = new Fixture();
        _sut = new InMemoryLogStorage();
    }

    [Fact]
    public async Task InsertLogsAsync_WithValidLogs_ShouldStoreSuccessfully()
    {
        // Arrange
        var logs = _fixture.CreateMany<LogEntry>(5).ToList();

        // Act
        await _sut.InsertLogsAsync(logs);

        // Assert
        var result = await _sut.GetLogsAsync(10);
        result.Should().HaveCount(5);
        result.Should().BeEquivalentTo(logs, options => options.WithStrictOrdering());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public async Task InsertLogsAsync_WithVariousLogCounts_ShouldHandleCorrectly(int logCount)
    {
        // Arrange
        var logs = _fixture.CreateMany<LogEntry>(logCount).ToList();

        // Act
        await _sut.InsertLogsAsync(logs);

        // Assert
        var result = await _sut.GetLogsAsync(logCount + 10);
        result.Should().HaveCount(logCount);
    }

    [Fact]
    public async Task GetLogsAsync_WithLimitAndOffset_ShouldReturnPagedResults()
    {
        // Arrange
        var logs = _fixture.CreateMany<LogEntry>(20)
            .Select((log, index) => 
            {
                log.Timestamp = DateTimeOffset.UtcNow.AddMinutes(-index);
                return log;
            })
            .ToList();

        await _sut.InsertLogsAsync(logs);

        // Act
        var firstPage = await _sut.GetLogsAsync(5, 0);
        var secondPage = await _sut.GetLogsAsync(5, 5);

        // Assert
        firstPage.Should().HaveCount(5);
        secondPage.Should().HaveCount(5);
        firstPage.Should().NotIntersectWith(secondPage);
        
        // Verify ordering (newest first)
        firstPage.Should().BeInDescendingOrder(x => x.Timestamp);
        secondPage.Should().BeInDescendingOrder(x => x.Timestamp);
    }

    [Theory]
    [AutoData]
    public async Task SearchLogsAsync_WithMatchingTerm_ShouldReturnFilteredResults(string searchTerm)
    {
        // Arrange
        var matchingLogs = _fixture.Build<LogEntry>()
            .With(x => x.Body, $"This log contains {searchTerm} in the body")
            .CreateMany(3)
            .ToList();

        var nonMatchingLogs = _fixture.Build<LogEntry>()
            .With(x => x.Body, "This log does not contain the search term")
            .CreateMany(5)
            .ToList();

        await _sut.InsertLogsAsync(matchingLogs.Concat(nonMatchingLogs));

        // Act
        var result = await _sut.SearchLogsAsync(searchTerm);

        // Assert
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(log => 
            log.Body.Should().Contain(searchTerm, "all returned logs should contain the search term"));
    }

    [Fact]
    public async Task GetLogsAsync_WhenEmpty_ShouldReturnEmptyCollection()
    {
        // Act
        var result = await _sut.GetLogsAsync();

        // Assert
        result.Should().BeEmpty();
        result.Should().NotBeNull();
    }

    [Fact] 
    public async Task InsertLogsAsync_WithConcurrentWrites_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var totalLogs = 0;

        // Act - Simulate concurrent writes
        for (int i = 0; i < 10; i++)
        {
            var logs = _fixture.CreateMany<LogEntry>(10).ToList();
            totalLogs += logs.Count;
            tasks.Add(_sut.InsertLogsAsync(logs));
        }

        await Task.WhenAll(tasks);

        // Assert
        var result = await _sut.GetLogsAsync(totalLogs);
        result.Should().HaveCount(totalLogs, "all logs should be stored despite concurrent writes");
    }
}