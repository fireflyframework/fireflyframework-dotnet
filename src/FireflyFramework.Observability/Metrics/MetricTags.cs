// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
