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
