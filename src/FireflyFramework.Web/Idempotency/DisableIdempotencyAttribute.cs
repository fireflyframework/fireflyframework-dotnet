namespace FireflyFramework.Web.Idempotency;

/// <summary>
/// Marker for an action method to opt out of idempotency caching. Mirrors Java
/// <c>@DisableIdempotency</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class DisableIdempotencyAttribute : Attribute
{
}
