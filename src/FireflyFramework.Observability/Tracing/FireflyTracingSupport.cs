using System.Diagnostics;

namespace FireflyFramework.Observability.Tracing;

/// <summary>
/// Helper for creating spans around async operations. Mirrors Java
/// <c>FireflyTracingSupport</c>; uses <see cref="ActivitySource"/> which automatically
/// propagates context across <c>async</c>/<c>await</c> boundaries.
/// </summary>
public sealed class FireflyTracingSupport
{
    private readonly ActivitySource _source;

    public FireflyTracingSupport(string sourceName) => _source = new ActivitySource(sourceName);

    public ActivitySource Source => _source;

    public async Task<T> Trace<T>(string spanName, Func<Task<T>> work, IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        using var activity = _source.StartActivity(spanName, ActivityKind.Internal);
        if (activity is not null && tags is not null)
        {
            foreach (var t in tags)
            {
                activity.SetTag(t.Key, t.Value);
            }
        }

        try
        {
            var result = await work().ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);
            activity?.SetTag("exception.message", ex.Message);
            throw;
        }
    }
}
