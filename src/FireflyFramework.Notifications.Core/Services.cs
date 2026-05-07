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

using Scriban;

namespace FireflyFramework.Notifications.Core;

public interface INotificationTemplateEngine
{
    Task<string> RenderAsync(string templateId, IDictionary<string, object?> variables, CancellationToken ct = default);
}

public sealed class ScribanTemplateEngine : INotificationTemplateEngine
{
    private readonly Func<string, CancellationToken, Task<string>> _loader;

    public ScribanTemplateEngine(Func<string, CancellationToken, Task<string>> loader) => _loader = loader;

    public async Task<string> RenderAsync(string templateId, IDictionary<string, object?> variables, CancellationToken ct = default)
    {
        var source = await _loader(templateId, ct).ConfigureAwait(false);
        var template = Template.Parse(source, templateId);
        return await template.RenderAsync(variables).ConfigureAwait(false);
    }
}

public sealed class EmailService
{
    private readonly IEmailProvider _provider;
    private readonly INotificationTemplateEngine? _engine;

    public EmailService(IEmailProvider provider, INotificationTemplateEngine? engine = null)
    {
        _provider = provider;
        _engine = engine;
    }

    public Task<EmailResponse> SendAsync(EmailRequest request, CancellationToken ct = default) =>
        _provider.SendEmailAsync(request, ct);

    public async Task<EmailResponse> SendTemplateAsync(EmailTemplateRequest request, string from, string subject, CancellationToken ct = default)
    {
        if (_engine is null)
        {
            return new EmailResponse(null, false, "No template engine configured");
        }

        var html = await _engine.RenderAsync(request.TemplateId, request.Variables, ct).ConfigureAwait(false);
        var email = new EmailRequest(from, request.Recipients, null, null, subject, null, html);
        return await _provider.SendEmailAsync(email, ct).ConfigureAwait(false);
    }
}

public sealed class SmsService
{
    private readonly ISmsProvider _provider;
    public SmsService(ISmsProvider provider) => _provider = provider;
    public Task<SmsResponse> SendAsync(SmsRequest request, CancellationToken ct = default) =>
        _provider.SendSmsAsync(request, ct);
}

public sealed class PushService
{
    private readonly IPushProvider _provider;
    public PushService(IPushProvider provider) => _provider = provider;
    public Task<PushNotificationResponse> SendAsync(PushNotificationRequest request, CancellationToken ct = default) =>
        _provider.SendPushAsync(request, ct);
}

public interface INotificationPreferenceService
{
    Task<NotificationPreferenceDto?> GetAsync(string userId, CancellationToken ct = default);
    Task UpdateAsync(NotificationPreferenceDto preferences, CancellationToken ct = default);
    Task<bool> IsChannelEnabledAsync(string userId, string channel, CancellationToken ct = default);
}
