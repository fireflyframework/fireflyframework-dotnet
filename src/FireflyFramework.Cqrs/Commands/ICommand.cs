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

using FireflyFramework.Cqrs.Authorization;
using FireflyFramework.Cqrs.Context;
using FireflyFramework.Cqrs.Validation;

namespace FireflyFramework.Cqrs.Commands;

/// <summary>
/// Marker for write-side messages. Mirrors Java <c>Command&lt;R&gt;</c>: the type
/// argument <typeparamref name="TResult"/> defines the response shape.
/// </summary>
public interface ICommand<out TResult>
{
    Guid CommandId => Guid.NewGuid();
    DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
    string? CorrelationId => null;
    string? InitiatedBy => null;
    IReadOnlyDictionary<string, object?> Metadata => EmptyMetadata;

    Task<ValidationResult> ValidateAsync(CancellationToken ct = default) => Task.FromResult(ValidationResult.Successful());
    Task<AuthorizationResult> AuthorizeAsync(ExecutionContext context, CancellationToken ct = default) => Task.FromResult(AuthorizationResult.Allowed());

    private static readonly IReadOnlyDictionary<string, object?> EmptyMetadata = new Dictionary<string, object?>();
}
