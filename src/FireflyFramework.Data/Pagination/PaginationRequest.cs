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
