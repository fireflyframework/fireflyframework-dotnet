namespace FireflyFramework.Observability.Metrics;

/// <summary>Standard low-cardinality tags. Mirrors Java <c>MetricTags</c>.</summary>
public static class MetricTags
{
    public const string Status = "status";
    public const string ErrorType = "error.type";
    public const string Operation = "operation";
    public const string CommandType = "command.type";
    public const string QueryType = "query.type";
    public const string EventType = "event.type";
    public const string WorkflowId = "workflow.id";
    public const string StepId = "step.id";
    public const string Provider = "provider";
    public const string Destination = "destination";
    public const string PublisherType = "publisher.type";
    public const string ConsumerType = "consumer.type";
    public const string TransactionType = "transaction.type";
    public const string AggregateType = "aggregate.type";
    public const string JobName = "job.name";
    public const string JobStage = "job.stage";
    public const string ClientType = "client.type";
    public const string WebhookType = "webhook.type";
    public const string NotificationType = "notification.type";
    public const string Channel = "channel";

    public const string Success = "success";
    public const string Failure = "failure";
    public const string Timeout = "timeout";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";
}
