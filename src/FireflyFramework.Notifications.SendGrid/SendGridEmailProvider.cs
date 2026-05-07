// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using FireflyFramework.Notifications;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace FireflyFramework.Notifications.SendGrid;

public sealed class SendGridOptions
{
    public const string SectionName = "Firefly:Notifications:SendGrid";
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Override the default <c>https://api.sendgrid.com</c> host. Used by tests to point
    /// the SendGrid SDK at WireMock; production deployments leave it unset.
    /// </summary>
    public string? Host { get; set; }
}

public sealed class SendGridEmailProvider : IEmailProvider
{
    private readonly ISendGridClient _client;

    /// <summary>Production constructor — builds a real <see cref="SendGridClient"/>.</summary>
    public SendGridEmailProvider(IOptions<SendGridOptions> options)
        : this(BuildClient(options.Value)) { }

    /// <summary>Testing constructor — accepts an explicit <see cref="ISendGridClient"/>.</summary>
    public SendGridEmailProvider(ISendGridClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    private static SendGridClient BuildClient(SendGridOptions opt) => opt.Host is null
        ? new SendGridClient(opt.ApiKey)
        : new SendGridClient(new SendGridClientOptions { ApiKey = opt.ApiKey, Host = opt.Host });

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
