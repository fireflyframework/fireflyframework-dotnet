using FireflyFramework.Notifications;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace FireflyFramework.Notifications.SendGrid;

public sealed class SendGridOptions
{
    public const string SectionName = "Firefly:Notifications:SendGrid";
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class SendGridEmailProvider : IEmailProvider
{
    private readonly SendGridClient _client;

    public SendGridEmailProvider(IOptions<SendGridOptions> options)
    {
        _client = new SendGridClient(options.Value.ApiKey);
    }

    public async Task<EmailResponse> SendEmailAsync(EmailRequest request, CancellationToken ct = default)
    {
        var msg = new SendGridMessage
        {
            From = new EmailAddress(request.From),
            Subject = request.Subject,
            PlainTextContent = request.Text,
            HtmlContent = request.Html,
        };

        msg.AddTos(request.To.Select(e => new EmailAddress(e)).ToList());
        if (request.Cc?.Count > 0) msg.AddCcs(request.Cc.Select(e => new EmailAddress(e)).ToList());
        if (request.Bcc?.Count > 0) msg.AddBccs(request.Bcc.Select(e => new EmailAddress(e)).ToList());
        if (request.Attachments is not null)
        {
            foreach (var att in request.Attachments)
            {
                msg.AddAttachment(att.FileName, Convert.ToBase64String(att.Data), att.ContentType);
            }
        }

        var response = await _client.SendEmailAsync(msg, ct).ConfigureAwait(false);
        var success = (int)response.StatusCode is >= 200 and < 300;
        return new EmailResponse(
            response.Headers.FirstOrDefault(h => h.Key == "X-Message-Id").Value?.FirstOrDefault(),
            success,
            success ? null : await response.Body.ReadAsStringAsync(ct).ConfigureAwait(false));
    }
}
