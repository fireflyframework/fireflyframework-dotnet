using FireflyFramework.Notifications;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace FireflyFramework.Notifications.Twilio;

public sealed class TwilioOptions
{
    public const string SectionName = "Firefly:Notifications:Twilio";
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string DefaultFromNumber { get; set; } = string.Empty;
}

public sealed class TwilioSmsProvider : ISmsProvider
{
    private readonly TwilioOptions _opt;

    public TwilioSmsProvider(IOptions<TwilioOptions> options)
    {
        _opt = options.Value;
        TwilioClient.Init(_opt.AccountSid, _opt.AuthToken);
    }

    public async Task<SmsResponse> SendSmsAsync(SmsRequest request, CancellationToken ct = default)
    {
        try
        {
            var message = await MessageResource.CreateAsync(
                from: new PhoneNumber(request.FromNumber ?? _opt.DefaultFromNumber),
                to: new PhoneNumber(request.PhoneNumber),
                body: request.Message).ConfigureAwait(false);
            return new SmsResponse(message.Sid, true, null);
        }
        catch (Exception ex)
        {
            return new SmsResponse(null, false, ex.Message);
        }
    }
}
