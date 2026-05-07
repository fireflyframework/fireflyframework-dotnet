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
