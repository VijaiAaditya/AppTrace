using Grpc.Core;
using OpenTelemetry.Proto.Collector.Trace.V1;
using AppTrace.Common.Models;
using AppTrace.Storage;

namespace AppTrace.Collector.Services;

public class OtlpTraceService : TraceService.TraceServiceBase
{
    private readonly ILogger<OtlpTraceService> _logger;
    private readonly ITraceStorage _traceStorage;

    public OtlpTraceService(ILogger<OtlpTraceService> logger, ITraceStorage traceStorage)
    {
        _logger = logger;
        _traceStorage = traceStorage;
    }

    public override async Task<ExportTraceServiceResponse> Export(
        ExportTraceServiceRequest request, 
        ServerCallContext context)
    {
        try
        {
            var traces = new List<TraceEntry>();

            foreach (var resourceSpan in request.ResourceSpans)
            {
                var serviceName = ExtractServiceName(resourceSpan.Resource);
                
                foreach (var scopeSpan in resourceSpan.ScopeSpans)
                {
                    foreach (var span in scopeSpan.Spans)
                    {
                        var trace = new TraceEntry
                        {
                            Id = Guid.NewGuid(),
                            TraceId = Convert.ToHexString(span.TraceId.ToByteArray()),
                            SpanId = Convert.ToHexString(span.SpanId.ToByteArray()),
                            ParentSpanId = span.ParentSpanId.IsEmpty ? string.Empty : Convert.ToHexString(span.ParentSpanId.ToByteArray()),
                            Name = span.Name,
                            StartTime = FromUnixTimeNanoseconds(span.StartTimeUnixNano),
                            EndTime = FromUnixTimeNanoseconds(span.EndTimeUnixNano),
                            Attributes = ExtractAttributes(span.Attributes, serviceName),
                            Status = span.Status?.Code.ToString() ?? "OK"
                        };

                        traces.Add(trace);
                    }
                }
            }

            await _traceStorage.InsertTracesAsync(traces);

            _logger.LogInformation("Processed {Count} trace spans", traces.Count);

            return new ExportTraceServiceResponse
            {
                PartialSuccess = new ExportTracePartialSuccess
                {
                    RejectedSpans = 0,
                    ErrorMessage = string.Empty
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing trace export request");
            return new ExportTraceServiceResponse
            {
                PartialSuccess = new ExportTracePartialSuccess
                {
                    RejectedSpans = request.ResourceSpans.Sum(rs => rs.ScopeSpans.Sum(ss => ss.Spans.Count)),
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
}