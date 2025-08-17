using AutoFixture;
using AutoFixture.Kernel;
using System.Text.Json;

namespace AppTrace.Tests.Common;

/// <summary>
/// Common test utilities and fixtures for all test projects
/// </summary>
public static class TestFixtureExtensions
{
    /// <summary>
    /// Creates a fixture configured for AppTrace domain objects
    /// </summary>
    public static IFixture CreateAppTraceFixture()
    {
        var fixture = new Fixture();
        
        // Configure realistic data generation
        fixture.Customize<DateTimeOffset>(c => c.FromFactory(() => 
            DateTimeOffset.UtcNow.AddMinutes(-Random.Shared.Next(0, 1440)))); // Last 24 hours
            
        fixture.Customize<Guid>(c => c.FromFactory(() => Guid.NewGuid()));
        
        // Configure realistic log severities
        fixture.Customize<string>(c => c.FromFactory(() => 
            new[] { "DEBUG", "INFO", "WARN", "ERROR", "FATAL" }[Random.Shared.Next(0, 5)]));
            
        return fixture;
    }
}

/// <summary>
/// Test constants used across test projects
/// </summary>
public static class TestConstants
{
    public const string TestServiceName = "test-service";
    public const string TestTraceId = "12345678901234567890123456789012";
    public const string TestSpanId = "1234567890123456";
    public const string TestEnvironment = "test";
}

/// <summary>
/// Helper methods for creating test data
/// </summary>
public static class TestDataHelper
{
    public static Dictionary<string, object> CreateTestAttributes(string serviceName = TestConstants.TestServiceName)
    {
        return new Dictionary<string, object>
        {
            ["service.name"] = serviceName,
            ["service.version"] = "1.0.0",
            ["deployment.environment"] = TestConstants.TestEnvironment,
            ["host.name"] = Environment.MachineName
        };
    }
    
    public static byte[] CreateTestTraceId()
    {
        return Convert.FromHexString(TestConstants.TestTraceId);
    }
    
    public static byte[] CreateTestSpanId()
    {
        return Convert.FromHexString(TestConstants.TestSpanId);
    }
}