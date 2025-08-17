using AppTrace.Storage;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;
using AppTrace.Common.Models;
using AutoFixture;

namespace AppTrace.Integration.Tests;

/// <summary>
/// Integration tests using real PostgreSQL database via Testcontainers
/// This shows how to test with real database for confidence in production behavior
/// </summary>
public class PostgreSqlStorageIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgreSqlContainer;
    private readonly IFixture _fixture;
    private readonly ILogger<PostgreSqlLogStorage> _mockLogger;
    private string _connectionString = string.Empty;

    public PostgreSqlStorageIntegrationTests()
    {
        _postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("apptrace_test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();

        _fixture = new Fixture();
        _mockLogger = Substitute.For<ILogger<PostgreSqlLogStorage>>();
    }

    public async Task InitializeAsync()
    {
        await _postgreSqlContainer.StartAsync();
        _connectionString = _postgreSqlContainer.GetConnectionString();
        
        // Create the schema
        await CreateDatabaseSchema();
    }

    public async Task DisposeAsync()
    {
        await _postgreSqlContainer.DisposeAsync();
    }

    [Fact]
    public async Task PostgreSqlLogStorage_IntegrationTest_ShouldWorkEndToEnd()
    {
        // Arrange
        var storage = new PostgreSqlLogStorage(_connectionString, _mockLogger);
        var testLogs = _fixture.Build<LogEntry>()
            .With(x => x.Attributes, new Dictionary<string, object>
            {
                ["service.name"] = "integration-test-service",
                ["environment"] = "test"
            })
            .CreateMany(10)
            .ToList();

        // Act - Insert
        await storage.InsertLogsAsync(testLogs);

        // Assert - Retrieve
        var retrievedLogs = await storage.GetLogsAsync(20);
        
        retrievedLogs.Should().HaveCount(10);
        retrievedLogs.Should().BeEquivalentTo(testLogs, options => 
            options.Excluding(x => x.Attributes)); // JSONB serialization may differ slightly

        // Verify service names were extracted correctly
        retrievedLogs.Should().AllSatisfy(log =>
            log.Attributes.Should().ContainKey("service.name"));
    }

    [Fact]
    public async Task PostgreSqlLogStorage_SearchFunctionality_ShouldFilterCorrectly()
    {
        // Arrange
        var storage = new PostgreSqlLogStorage(_connectionString, _mockLogger);
        var searchTerm = "CRITICAL_ERROR_12345";
        
        var matchingLogs = _fixture.Build<LogEntry>()
            .With(x => x.Body, $"Application encountered {searchTerm} during processing")
            .CreateMany(3)
            .ToList();
            
        var nonMatchingLogs = _fixture.Build<LogEntry>()
            .With(x => x.Body, "Normal operation log message")
            .CreateMany(7)
            .ToList();

        await storage.InsertLogsAsync(matchingLogs.Concat(nonMatchingLogs));

        // Act
        var searchResults = await storage.SearchLogsAsync(searchTerm);

        // Assert
        searchResults.Should().HaveCount(3);
        searchResults.Should().AllSatisfy(log => 
            log.Body.Should().Contain(searchTerm));
    }

    [Fact]
    public async Task PostgreSqlLogStorage_ConcurrentWrites_ShouldHandleCorrectly()
    {
        // Arrange
        var storage = new PostgreSqlLogStorage(_connectionString, _mockLogger);
        var tasks = new List<Task>();
        const int batchCount = 5;
        const int logsPerBatch = 20;

        // Act - Simulate concurrent writes
        for (int i = 0; i < batchCount; i++)
        {
            var logs = _fixture.CreateMany<LogEntry>(logsPerBatch).ToList();
            tasks.Add(storage.InsertLogsAsync(logs));
        }

        await Task.WhenAll(tasks);

        // Assert
        var allLogs = await storage.GetLogsAsync(batchCount * logsPerBatch + 10);
        allLogs.Should().HaveCount(batchCount * logsPerBatch);
    }

    private async Task CreateDatabaseSchema()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var createTableSql = @"
            CREATE EXTENSION IF NOT EXISTS ""uuid-ossp"";
            
            CREATE TABLE IF NOT EXISTS logs (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                timestamp TIMESTAMPTZ NOT NULL,
                trace_id TEXT,
                span_id TEXT,
                severity TEXT,
                body TEXT,
                attributes JSONB,
                service_name TEXT NOT NULL DEFAULT 'unknown',
                created_at TIMESTAMPTZ DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_logs_timestamp ON logs(timestamp DESC);
            CREATE INDEX IF NOT EXISTS idx_logs_service_name ON logs(service_name);
            CREATE INDEX IF NOT EXISTS idx_logs_attributes_gin ON logs USING GIN (attributes);";

        using var command = new NpgsqlCommand(createTableSql, connection);
        await command.ExecuteNonQueryAsync();
    }
}