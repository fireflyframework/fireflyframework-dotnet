namespace FireflyFramework.Observability.Logging;

/// <summary>Standard logging-scope keys. Mirrors Java <c>MdcConstants</c>.</summary>
public static class MdcConstants
{
    public const string TraceId = "traceId";
    public const string SpanId = "spanId";
    public const string TransactionId = "transactionId";
    public const string TransactionIdHeader = "X-Transaction-Id";
    public const string UserId = "userId";
    public const string CorrelationId = "correlationId";
    public const string RequestId = "requestId";
    public const string ServiceName = "service.name";
    public const string AggregateType = "aggregate.type";
    public const string AggregateId = "aggregate.id";
}
