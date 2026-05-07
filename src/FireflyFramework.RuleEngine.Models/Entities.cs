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

using FireflyFramework.Data.Domain;
using FireflyFramework.RuleEngine.Interfaces;

namespace FireflyFramework.RuleEngine.Models;

public sealed class RuleDefinitionEntity : BaseEntity<Guid>
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string YamlContent { get; set; } = string.Empty;
    public string Version { get; set; } = "1";
    public bool IsActive { get; set; } = true;
    public string? Tags { get; set; }
}

public sealed class ConstantEntity : BaseEntity<Guid>
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RuleValueType DataType { get; set; } = RuleValueType.String;
}

public sealed class AuditTrailEntity : BaseEntity<Guid>
{
    public AuditEventType OperationType { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? RuleCode { get; set; }
    public string? UserId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? HttpMethod { get; set; }
    public string? Endpoint { get; set; }
    public string? RequestData { get; set; }
    public string? ResponseData { get; set; }
    public int? StatusCode { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long? ExecutionTimeMs { get; set; }
    public string? Metadata { get; set; }
    public string? SessionId { get; set; }
    public string? CorrelationId { get; set; }
}
