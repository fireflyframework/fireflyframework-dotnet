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

namespace FireflyFramework.Data.Pagination;

/// <summary>Pagination request DTO. Mirrors Java <c>PaginationRequest</c>.</summary>
public sealed class PaginationRequest
{
    public int PageNumber { get; set; }
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public SortDirection SortDirection { get; set; } = SortDirection.Asc;

    public int Skip => Math.Max(0, PageNumber) * Math.Max(1, PageSize);
}

public enum SortDirection { Asc, Desc }
