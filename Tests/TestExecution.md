# AppTrace Test Execution Scripts

## Run All Tests
```bash
# Run all tests with coverage
dotnet test --collect:"XPlat Code Coverage" --settings test.runsettings

# Run specific test project
dotnet test AppTrace.Storage.Tests

# Run tests with specific filter
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"

# Run tests in parallel
dotnet test --parallel

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults
```

## Performance Testing
```bash
# Run performance/load tests
dotnet test --filter "Category=Performance" --logger trx --results-directory TestResults

# Memory usage tests
dotnet test --filter "TestCategory=Memory" --collect:"dotnet memory"
```

## Integration Tests with Real Database
```bash
# Run integration tests (requires Docker for Testcontainers)
dotnet test AppTrace.Integration.Tests --filter "Category=Integration"

# Run with specific database version
dotnet test AppTrace.Integration.Tests --environment "DatabaseImage=postgres:15-alpine"
```

## Continuous Integration
```bash
# CI pipeline test command
dotnet test --no-build --verbosity normal --logger trx --results-directory TestResults --collect:"XPlat Code Coverage"
```

## Test Categories Used:
- **Unit**: Fast, isolated unit tests
- **Integration**: Tests with real dependencies (database, etc.)
- **Performance**: Load and performance tests
- **E2E**: End-to-end system tests
- **Smoke**: Quick validation tests