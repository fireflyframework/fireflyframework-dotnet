using System.Net.Http.Json;
using FireflyFramework.Notifications;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Notifications.Resend;

public sealed class ResendOptions
{
    public const string SectionName = "Firefly:Notifications:Resend";
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.resend.com";
}

public sealed class ResendEmailProvider : IEmailProvider
{
    private readonly HttpClient _http;
    private readonly ResendOptions _opt;

    public ResendEmailProvider(HttpClient http, IOptions<ResendOptions> options)
    {
        _http = http;
        _opt = options.Value;
        _http.BaseAddress ??= new Uri(_opt.BaseUrl);
        _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _opt.ApiKey);
    }

    public async Task<EmailResponse> SendEmailAsync(EmailRequest request, CancellationToken ct = default)
    {
        var payload = new
        {
            from = request.From,
            to = request.To,
            cc = request.Cc,
            bcc = request.Bcc,
            subject = request.Subject,
            text = request.Text,
            html = request.Html,
        };

        var resp = await _http.PostAsJsonAsync("/emails", payload, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var msg = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return new EmailResponse(null, false, msg);
        }

        var doc = await resp.Content.ReadFromJsonAsync<Dictionary<string, object?>>(cancellationToken: ct).ConfigureAwait(false);
        return new EmailResponse(doc?["id"]?.ToString(), true, null);
    }
}
