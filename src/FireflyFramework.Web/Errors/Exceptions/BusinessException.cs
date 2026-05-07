using FireflyFramework.Web.Errors.Models;

namespace FireflyFramework.Web.Errors.Exceptions;

/// <summary>422 Unprocessable Entity — business-rule violation.</summary>
public class BusinessException : ServiceException
{
    public BusinessException(string message, string errorCode = "BUSINESS_RULE_VIOLATION", Exception? cause = null)
        : base(message, 422, errorCode, ErrorCategory.Business, ErrorSeverity.Medium, false, null, cause) { }
}
