using Grpc.Core;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using AppTrace.Common.Models;
using AppTrace.Storage;

namespace AppTrace.Collector.Services;

public class OtlpMetricsService : MetricsService.MetricsServiceBase
{
    private readonly ILogger<OtlpMetricsService> _logger;
    private readonly IMetricStorage _metricStorage;

    public OtlpMetricsService(ILogger<OtlpMetricsService> logger, IMetricStorage metricStorage)
    {
        _logger = logger;
        _metricStorage = metricStorage;
    }

    public override async Task<ExportMetricsServiceResponse> Export(
        ExportMetricsServiceRequest request, 
        ServerCallContext context)
    {
        try
        {
            var metrics = new List<MetricEntry>();

            foreach (var resourceMetric in request.ResourceMetrics)
            {
                var serviceName = ExtractServiceName(resourceMetric.Resource);
                
                foreach (var scopeMetric in resourceMetric.ScopeMetrics)
                {
                    foreach (var metric in scopeMetric.Metrics)
                    {
                        // Process different metric types
                        if (metric.Gauge != null)
                        {
                            foreach (var dataPoint in metric.Gauge.DataPoints)
                            {
                                metrics.Add(CreateMetricEntry(metric.Name, dataPoint, serviceName));
                            }
                        }
                        else if (metric.Sum != null)
                        {
                            foreach (var dataPoint in metric.Sum.DataPoints)
                            {
                                metrics.Add(CreateMetricEntry(metric.Name, dataPoint, serviceName));
                            }
                        }
                        else if (metric.Histogram != null)
                        {
                            foreach (var dataPoint in metric.Histogram.DataPoints)
                            {
                                metrics.Add(CreateHistogramMetricEntry(metric.Name, dataPoint, serviceName));
                            }
                        }
                    }
                }
            }

            await _metricStorage.InsertMetricsAsync(metrics);

            _logger.LogInformation("Processed {Count} metric data points", metrics.Count);

            return new ExportMetricsServiceResponse
            {
                PartialSuccess = new ExportMetricsPartialSuccess
                {
                    RejectedDataPoints = 0,
                    ErrorMessage = string.Empty
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing metrics export request");
            return new ExportMetricsServiceResponse
            {
                PartialSuccess = new ExportMetricsPartialSuccess
                {
                    RejectedDataPoints = CountDataPoints(request),
                    ErrorMessage = ex.Message
                }
            };
        }
    }

    private static DateTimeOffset FromUnixTimeNanoseconds(ulong nanoseconds)
    {
        // Convert nanoseconds to ticks (1 tick = 100 nanoseconds)
        var ticks = (long)(nanoseconds / 100);
        return DateTimeOffset.FromUnixTimeMilliseconds(0).AddTicks(ticks);
    }

    private static string ExtractServiceName(Resource resource)
    {
        var serviceNameAttr = resource.Attributes.FirstOrDefault(attr => attr.Key == "service.name");
        return serviceNameAttr?.Value?.StringValue ?? "unknown-service";
    }

    private static MetricEntry CreateMetricEntry(string name, NumberDataPoint dataPoint, string serviceName)
    {
        var value = dataPoint.ValueCase switch
        {
            NumberDataPoint.ValueOneofCase.AsDouble => dataPoint.AsDouble,
            NumberDataPoint.ValueOneofCase.AsInt => dataPoint.AsInt,
            _ => 0.0
        };

        return new MetricEntry
        {
            Id = Guid.NewGuid(),
            Name = name,
            Timestamp = FromUnixTimeNanoseconds(dataPoint.TimeUnixNano),
            Value = value,
            Attributes = ExtractAttributes(dataPoint.Attributes, serviceName)
        };
    }

    private static MetricEntry CreateHistogramMetricEntry(string name, HistogramDataPoint dataPoint, string serviceName)
    {
        return new MetricEntry
        {
            Id = Guid.NewGuid(),
            Name = $"{name}_histogram",
            Timestamp = FromUnixTimeNanoseconds(dataPoint.TimeUnixNano),
            Value = dataPoint.Sum,
            Attributes = ExtractHistogramAttributes(dataPoint, serviceName)
        };
    }

    private static Dictionary<string, object> ExtractAttributes(IEnumerable<KeyValue> attributes, string serviceName)
    {
        var result = new Dictionary<string, object>
        {
            ["service.name"] = serviceName
        };

        foreach (var attr in attributes)
        {
            result[attr.Key] = ExtractValue(attr.Value);
        }

        return result;
    }

    private static Dictionary<string, object> ExtractHistogramAttributes(HistogramDataPoint dataPoint, string serviceName)
    {
        var result = ExtractAttributes(dataPoint.Attributes, serviceName);
        result["histogram.count"] = dataPoint.Count;
        result["histogram.sum"] = dataPoint.Sum;
        return result;
    }

    private static object ExtractValue(AnyValue value)
    {
        return value.ValueCase switch
        {
            AnyValue.ValueOneofCase.StringValue => value.StringValue,
            AnyValue.ValueOneofCase.BoolValue => value.BoolValue,
            AnyValue.ValueOneofCase.IntValue => value.IntValue,
            AnyValue.ValueOneofCase.DoubleValue => value.DoubleValue,
            AnyValue.ValueOneofCase.BytesValue => Convert.ToHexString(value.BytesValue.ToByteArray()),
            _ => value.ToString()
        };
    }

    private static long CountDataPoints(ExportMetricsServiceRequest request)
    {
        return request.ResourceMetrics
            .SelectMany(rm => rm.ScopeMetrics)
            .SelectMany(sm => sm.Metrics)
            .Sum(m => 
                (m.Gauge?.DataPoints.Count ?? 0) + 
                (m.Sum?.DataPoints.Count ?? 0) + 
                (m.Histogram?.DataPoints.Count ?? 0));
    }
}