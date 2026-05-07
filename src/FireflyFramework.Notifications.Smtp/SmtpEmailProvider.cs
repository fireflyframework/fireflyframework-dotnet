// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using FireflyFramework.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Notifications.Smtp;

public sealed class SmtpOptions
{
    public const string SectionName = "Firefly:Notifications:Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? DefaultFrom { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

public sealed class SmtpEmailProvider : IEmailProvider
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailProvider> _logger;

    public SmtpEmailProvider(IOptions<SmtpOptions> options, ILogger<SmtpEmailProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailResponse> SendEmailAsync(EmailRequest request, CancellationToken ct = default)
    {
        try
        {
            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                Timeout = (int)_options.Timeout.TotalMilliseconds,
            };
            if (!string.IsNullOrEmpty(_options.Username))
                client.Credentials = new NetworkCredential(_options.Username, _options.Password);

            using var msg = new MailMessage
            {
                From = new MailAddress(string.IsNullOrEmpty(request.From) ? _options.DefaultFrom ?? string.Empty : request.From),
                Subject = request.Subject,
                Body = request.Html ?? request.Text ?? string.Empty,
                IsBodyHtml = !string.IsNullOrEmpty(request.Html),
            };
            foreach (var to in request.To) msg.To.Add(to);
            if (request.Cc is not null) foreach (var cc in request.Cc) msg.CC.Add(cc);
            if (request.Bcc is not null) foreach (var bcc in request.Bcc) msg.Bcc.Add(bcc);
            if (request.Attachments is not null)
            {
                foreach (var att in request.Attachments)
                    msg.Attachments.Add(new Attachment(new MemoryStream(att.Data), att.FileName, att.ContentType));
            }

            await client.SendMailAsync(msg, ct).ConfigureAwait(false);
            return new EmailResponse(MessageId: msg.Headers["Message-ID"], Success: true, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed");
            return new EmailResponse(MessageId: null, Success: false, ErrorMessage: ex.Message);
        }
    }
}

public static class SmtpServiceCollectionExtensions
{
    public static IServiceCollection AddSmtpEmailProvider(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<SmtpOptions>().Bind(config.GetSection(SmtpOptions.SectionName));
        services.TryAddSingleton<IEmailProvider, SmtpEmailProvider>();
        return services;
    }
}
