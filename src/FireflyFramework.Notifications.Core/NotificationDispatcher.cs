using Microsoft.Extensions.Logging;

namespace FireflyFramework.Notifications.Core;

/// <summary>
/// Single entry point for sending notifications. Routes a request to the right channel
/// (email / SMS / push) and respects per-user preferences.
/// Mirrors Java <c>NotificationDispatcher</c>.
/// </summary>
public sealed class NotificationDispatcher
{
    private readonly EmailService? _email;
    private readonly SmsService? _sms;
    private readonly PushService? _push;
    private readonly INotificationPreferenceService? _preferences;
    private readonly ILogger<NotificationDispatcher> _log;

    public NotificationDispatcher(
        ILogger<NotificationDispatcher> log,
        EmailService? email = null,
        SmsService? sms = null,
        PushService? push = null,
        INotificationPreferenceService? preferences = null)
    {
        _log = log;
        _email = email;
        _sms = sms;
        _push = push;
        _preferences = preferences;
    }

    public async Task<EmailResponse> SendEmailAsync(string userId, EmailRequest request, CancellationToken ct = default)
    {
        if (_email is null) return new EmailResponse(null, false, "no email provider registered");
        if (await BlockedByPreferences(userId, "email", ct).ConfigureAwait(false))
        {
            return new EmailResponse(null, false, "email channel disabled by user preferences");
        }

        return await _email.SendAsync(request, ct).ConfigureAwait(false);
    }

    public async Task<SmsResponse> SendSmsAsync(string userId, SmsRequest request, CancellationToken ct = default)
    {
        if (_sms is null) return new SmsResponse(null, false, "no sms provider registered");
        if (await BlockedByPreferences(userId, "sms", ct).ConfigureAwait(false))
        {
            return new SmsResponse(null, false, "sms channel disabled by user preferences");
        }

        return await _sms.SendAsync(request, ct).ConfigureAwait(false);
    }

    public async Task<PushNotificationResponse> SendPushAsync(string userId, PushNotificationRequest request, CancellationToken ct = default)
    {
        if (_push is null) return new PushNotificationResponse(null, false, "no push provider registered");
        if (await BlockedByPreferences(userId, "push", ct).ConfigureAwait(false))
        {
            return new PushNotificationResponse(null, false, "push channel disabled by user preferences");
        }

        return await _push.SendAsync(request, ct).ConfigureAwait(false);
    }

    private async Task<bool> BlockedByPreferences(string userId, string channel, CancellationToken ct)
    {
        if (_preferences is null) return false;
        return !await _preferences.IsChannelEnabledAsync(userId, channel, ct).ConfigureAwait(false);
    }
}

/// <summary>Default in-memory <see cref="INotificationPreferenceService"/>.</summary>
public sealed class InMemoryNotificationPreferenceService : INotificationPreferenceService
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, NotificationPreferenceDto> _store = new();

    public Task<NotificationPreferenceDto?> GetAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(userId, out var p) ? p : null);

    public Task UpdateAsync(NotificationPreferenceDto preferences, CancellationToken ct = default)
    {
        _store[preferences.UserId] = preferences;
        return Task.CompletedTask;
    }

    public Task<bool> IsChannelEnabledAsync(string userId, string channel, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(userId, out var p)) return Task.FromResult(true);
        return Task.FromResult(channel.ToLowerInvariant() switch
        {
            "email" => p.EmailEnabled,
            "sms"   => p.SmsEnabled,
            "push"  => p.PushEnabled,
            _       => true,
        });
    }
}
