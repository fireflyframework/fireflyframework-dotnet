using FireflyFramework.Web.Errors.Exceptions;
using Microsoft.AspNetCore.Http;

namespace FireflyFramework.Web.Errors.Converters;

/// <summary>
/// SPI for converting framework / standard exceptions into the
/// <see cref="ServiceException"/> hierarchy understood by
/// <see cref="Middleware.GlobalExceptionHandlerMiddleware"/>. Mirrors Java
/// <c>ExceptionConverter&lt;T&gt;</c>.
/// </summary>
public interface IExceptionConverter
{
    /// <summary>Concrete exception type this converter handles.</summary>
    Type ExceptionType { get; }

    /// <summary>Translate an exception into a service-level exception.</summary>
    ServiceException Convert(Exception exception, HttpContext context);
}
