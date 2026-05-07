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

using FireflyFramework.Data.Pagination;

namespace FireflyFramework.Data.Filters;

/// <summary>
/// Generic filter request: equality / collection / range / null / not-null filters,
/// plus pagination and per-request options. Mirrors Java <c>FilterRequest&lt;T&gt;</c>.
/// </summary>
public sealed class FilterRequest<T>
{
    public const string NullValue = "__FIREFLY_NULL__";
    public const string NotNullValue = "__FIREFLY_NOT_NULL__";

    public Dictionary<string, object?> Filters { get; set; } = new();

    public RangeFilter RangeFilters { get; set; } = new();

    public PaginationRequest Pagination { get; set; } = new();

    public FilterOptions Options { get; set; } = new();

    public static void SetNullFilter(IDictionary<string, object?> filters, string key) => filters[key] = NullValue;
    public static void SetNotNullFilter(IDictionary<string, object?> filters, string key) => filters[key] = NotNullValue;
}

public sealed class FilterOptions
{
    public bool CaseInsensitiveStrings { get; set; }
    public bool IncludeInheritedFields { get; set; }
}
