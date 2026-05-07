using System;
using System.Collections.Generic;

namespace FireflyFramework.Kernel.Exceptions;

/// <summary>
/// Infrastructure-level error: database, cache, messaging, networking. Mirrors
/// <c>org.fireflyframework.kernel.exception.FireflyInfrastructureException</c>.
/// </summary>
public class FireflyInfrastructureException : FireflyException
{
    private const string DefaultCode = "FIREFLY_INFRASTRUCTURE_ERROR";

    public FireflyInfrastructureException(string message)
        : base(message, DefaultCode) { }

    public FireflyInfrastructureException(string message, string errorCode)
        : base(message, errorCode) { }

    public FireflyInfrastructureException(string message, Exception? cause)
        : base(message, DefaultCode, cause) { }

    public FireflyInfrastructureException(string message, string errorCode, Exception? cause)
        : base(message, errorCode, cause) { }

    public FireflyInfrastructureException(
        string message,
        string errorCode,
        IDictionary<string, object?>? context,
        Exception? cause)
        : base(message, errorCode, context, cause) { }
}
