namespace FireflyFramework.Utils.Annotations;

/// <summary>
/// Marks an id-like field as filterable for the generic filter engine in
/// <c>FireflyFramework.Data</c>. Mirrors Java <c>@FilterableId</c>: by default the
/// engine excludes properties whose name ends with "Id"; this attribute opts back in
/// — exact-match only, no LIKE/range.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class FilterableIdAttribute : Attribute
{
}
