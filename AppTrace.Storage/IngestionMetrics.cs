using System.Collections.Concurrent;
using System.Diagnostics;

namespace AppTrace.Storage;

/// <summary>
/// Lightweight, allocation-conscious in-process counters for AppTrace's own
/// ingestion pipeline (self-observability). Uses Interlocked/concurrent
/// primitives only - no locks on the hot insert path.
/// </summary>
public sealed class IngestionMetrics
{
    private long _batchesProcessed;
    private long _itemsInserted;
    private long _retryCount;
    private long _fallbackCount;
    private long _failureCount;
    private readonly ConcurrentQueue<double> _recentLatenciesMs = new();
    private const int MaxRecentSamples = 500;

    public void RecordBatch(int itemCount, TimeSpan elapsed)
    {
        Interlocked.Increment(ref _batchesProcessed);
        Interlocked.Add(ref _itemsInserted, itemCount);

        _recentLatenciesMs.Enqueue(elapsed.TotalMilliseconds);
        while (_recentLatenciesMs.Count > MaxRecentSamples && _recentLatenciesMs.TryDequeue(out _)) { }
    }

    public void RecordRetry() => Interlocked.Increment(ref _retryCount);
    public void RecordFallback() => Interlocked.Increment(ref _fallbackCount);
    public void RecordFailure() => Interlocked.Increment(ref _failureCount);

    public IngestionMetricsSnapshot GetSnapshot()
    {
        var samples = _recentLatenciesMs.ToArray();
        Array.Sort(samples);

        return new IngestionMetricsSnapshot
        {
            BatchesProcessed = Interlocked.Read(ref _batchesProcessed),
            ItemsInserted = Interlocked.Read(ref _itemsInserted),
            RetryCount = Interlocked.Read(ref _retryCount),
            FallbackCount = Interlocked.Read(ref _fallbackCount),
            FailureCount = Interlocked.Read(ref _failureCount),
            P50LatencyMs = Percentile(samples, 0.50),
            P95LatencyMs = Percentile(samples, 0.95),
            P99LatencyMs = Percentile(samples, 0.99)
        };
    }

    private static double Percentile(double[] sortedSamples, double percentile)
    {
        if (sortedSamples.Length == 0) return 0;
        var index = (int)Math.Ceiling(percentile * sortedSamples.Length) - 1;
        index = Math.Clamp(index, 0, sortedSamples.Length - 1);
        return sortedSamples[index];
    }
}

public sealed class IngestionMetricsSnapshot
{
    public long BatchesProcessed { get; init; }
    public long ItemsInserted { get; init; }
    public long RetryCount { get; init; }
    public long FallbackCount { get; init; }
    public long FailureCount { get; init; }
    public double P50LatencyMs { get; init; }
    public double P95LatencyMs { get; init; }
    public double P99LatencyMs { get; init; }
}
