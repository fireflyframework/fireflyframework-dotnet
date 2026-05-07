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

namespace FireflyFramework.Data.Domain;

/// <summary>Marker interface for entities with a typed identifier. Mirrors Java <c>BaseEntity</c>.</summary>
public interface IBaseEntity<out TId>
{
    TId Id { get; }
}

/// <summary>Adds optimistic concurrency token (RowVersion) and audit timestamps.</summary>
public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
    string? CreatedBy { get; set; }
    string? UpdatedBy { get; set; }
}

/// <summary>Marker for entities that participate in optimistic concurrency.</summary>
public interface IVersionedEntity
{
    /// <summary>Optimistic concurrency token. Mapped to RowVersion / xmin in EF Core.</summary>
    byte[]? RowVersion { get; set; }
}

/// <summary>Marker for soft-delete support.</summary>
public interface ISoftDeleteEntity
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}

/// <summary>Marker for tenant scoping.</summary>
public interface ITenantScopedEntity
{
    string TenantId { get; set; }
}
