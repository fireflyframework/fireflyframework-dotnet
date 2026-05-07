using System.Text.Json.Serialization;

namespace FireflyFramework.Web.Errors.Models;

/// <summary>Field-level validation failure. Mirrors Java <c>ErrorResponse.ValidationError</c>.</summary>
public sealed class ValidationError
{
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; }

    public ValidationError() { }

    public ValidationError(string field, string code, string message, Dictionary<string, object?>? metadata = null)
    {
        Field = field;
        Code = code;
        Message = message;
        Metadata = metadata;
    }
}
