using System;
using System.Collections.Generic;

namespace FireflyFramework.Kernel.Exceptions;

/// <summary>
/// Security-domain error: authentication or authorization failure. Mirrors
/// <c>org.fireflyframework.kernel.exception.FireflySecurityException</c>.
/// </summary>
public class FireflySecurityException : FireflyException
{
    private const string DefaultCode = "FIREFLY_SECURITY_ERROR";

    public FireflySecurityException(string message)
        : base(message, DefaultCode) { }

    public FireflySecurityException(string message, string errorCode)
        : base(message, errorCode) { }

    public FireflySecurityException(string message, Exception? cause)
        : base(message, DefaultCode, cause) { }

    public FireflySecurityException(string message, string errorCode, Exception? cause)
        : base(message, errorCode, cause) { }

    public FireflySecurityException(
        string message,
        string errorCode,
        IDictionary<string, object?>? context,
        Exception? cause)
        : base(message, errorCode, context, cause) { }
}
