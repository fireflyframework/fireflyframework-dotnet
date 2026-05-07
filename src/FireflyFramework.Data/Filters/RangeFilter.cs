namespace FireflyFramework.Data.Filters;

/// <summary>Range filter (from/to) for numeric, date or comparable fields. Mirrors Java <c>RangeFilter</c>.</summary>
public sealed class RangeFilter
{
    public Dictionary<string, Range> Ranges { get; set; } = new();

    public sealed class Range
    {
        public object? From { get; set; }
        public object? To { get; set; }
    }
}
