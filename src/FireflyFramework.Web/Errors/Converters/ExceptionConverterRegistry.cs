using FireflyFramework.Web.Errors.Exceptions;
using Microsoft.AspNetCore.Http;

namespace FireflyFramework.Web.Errors.Converters;

/// <summary>
/// Walks the registered <see cref="IExceptionConverter"/>s, returning the first one
/// whose target type matches the thrown exception. Mirrors Java
/// <c>ExceptionConverterService</c>.
/// </summary>
public sealed class ExceptionConverterRegistry
{
    private readonly IReadOnlyList<IExceptionConverter> _converters;

    public ExceptionConverterRegistry(IEnumerable<IExceptionConverter> converters)
    {
        _converters = converters.ToList();
    }

    public ServiceException? TryConvert(Exception exception, HttpContext context)
    {
        foreach (var converter in _converters)
        {
            if (converter.ExceptionType.IsInstanceOfType(exception))
            {
                return converter.Convert(exception, context);
            }
        }

        return null;
    }
}
