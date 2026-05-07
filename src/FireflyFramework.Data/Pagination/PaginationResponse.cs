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

/// <summary>Pagination response wrapping a page of items. Mirrors Java <c>PaginationResponse&lt;T&gt;</c>.</summary>
public sealed class PaginationResponse<T>
{
    public IReadOnlyList<T> Content { get; init; } = Array.Empty<T>();
    public long TotalElements { get; init; }
    public int TotalPages { get; init; }
    public int CurrentPage { get; init; }
    public int PageSize { get; init; }

    public static PaginationResponse<T> Empty(PaginationRequest request) => new()
    {
        Content = Array.Empty<T>(),
        TotalElements = 0,
        TotalPages = 0,
        CurrentPage = request.PageNumber,
        PageSize = request.PageSize,
    };
}
