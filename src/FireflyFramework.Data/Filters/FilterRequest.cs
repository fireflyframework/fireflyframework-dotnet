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
