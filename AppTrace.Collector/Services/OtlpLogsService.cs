using Grpc.Core;
using OpenTelemetry.Proto.Collector.Logs.V1;
using AppTrace.Common.Models;
using AppTrace.Storage;

namespace AppTrace.Collector.Services;

public class OtlpLogsService : LogsService.LogsServiceBase
{
    private readonly ILogger<OtlpLogsService> _logger;
    private readonly ILogStorage _logStorage;

    public OtlpLogsService(ILogger<OtlpLogsService> logger, ILogStorage logStorage)
    {
        _logger = logger;
        _logStorage = logStorage;
    }

    public override async Task<ExportLogsServiceResponse> Export(
        ExportLogsServiceRequest request, 
        ServerCallContext context)
    {
        try
        {
            var logs = new List<LogEntry>();

            foreach (var resourceLog in request.ResourceLogs)
            {
                var serviceName = ExtractServiceName(resourceLog.Resource);
                
                foreach (var scopeLog in resourceLog.ScopeLogs)
                {
                    foreach (var logRecord in scopeLog.LogRecords)
                    {
                        var log = new LogEntry
                        {
                            Id = Guid.NewGuid(),
                            Timestamp = FromUnixTimeNanoseconds(logRecord.TimeUnixNano),
                            TraceId = logRecord.TraceId.IsEmpty ? string.Empty : Convert.ToHexString(logRecord.TraceId.ToByteArray()),
                            SpanId = logRecord.SpanId.IsEmpty ? string.Empty : Convert.ToHexString(logRecord.SpanId.ToByteArray()),
                            Severity = logRecord.SeverityText,
                            Body = ExtractBody(logRecord.Body),
                            Attributes = ExtractAttributes(logRecord.Attributes, serviceName)
                        };

                        logs.Add(log);
                    }
                }
            }

            await _logStorage.InsertLogsAsync(logs);

            _logger.LogInformation("Processed {Count} log records", logs.Count);

            return new ExportLogsServiceResponse
            {
                PartialSuccess = new ExportLogsPartialSuccess
                {
                    RejectedLogRecords = 0,
                    ErrorMessage = string.Empty
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing logs export request");
            return new ExportLogsServiceResponse
            {
                PartialSuccess = new ExportLogsPartialSuccess
                {
                    RejectedLogRecords = request.ResourceLogs.Sum(rl => rl.ScopeLogs.Sum(sl => sl.LogRecords.Count)),
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

    private static string ExtractBody(AnyValue body)
    {
        return body?.ValueCase switch
        {
            AnyValue.ValueOneofCase.StringValue => body.StringValue,
            AnyValue.ValueOneofCase.IntValue => body.IntValue.ToString(),
            AnyValue.ValueOneofCase.DoubleValue => body.DoubleValue.ToString(),
            AnyValue.ValueOneofCase.BoolValue => body.BoolValue.ToString(),
            _ => body?.ToString() ?? string.Empty
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