using System.ServiceModel;
using System.ServiceModel.Channels;

namespace FireflyFramework.Client.Soap;

/// <summary>
/// Builds a SOAP client channel using <see cref="System.ServiceModel"/> (WCF Core).
/// Mirrors Java <c>SoapClientBuilder</c>: address + binding + optional WS-Security.
/// </summary>
public sealed class SoapClientBuilder<TChannel> where TChannel : class
{
    private string? _endpointAddress;
    private SoapTransport _transport = SoapTransport.Http;
    private TimeSpan _timeout = TimeSpan.FromSeconds(60);
    private string? _username;
    private string? _password;

    public static SoapClientBuilder<TChannel> Create() => new();

    public SoapClientBuilder<TChannel> WithEndpointAddress(string url) { _endpointAddress = url; return this; }

    public SoapClientBuilder<TChannel> WithTransport(SoapTransport transport) { _transport = transport; return this; }

    public SoapClientBuilder<TChannel> WithTimeout(TimeSpan timeout) { _timeout = timeout; return this; }

    public SoapClientBuilder<TChannel> WithBasicAuth(string username, string password)
    {
        _username = username;
        _password = password;
        return this;
    }

    /// <summary>Builds a channel for the contract type <typeparamref name="TChannel"/>.</summary>
    public TChannel Build()
    {
        if (_endpointAddress is null)
        {
            throw new InvalidOperationException("Endpoint address is required");
        }

        Binding binding = _transport switch
        {
            SoapTransport.Https => new BasicHttpsBinding
            {
                SendTimeout = _timeout,
                ReceiveTimeout = _timeout,
            },
            _ => new BasicHttpBinding
            {
                SendTimeout = _timeout,
                ReceiveTimeout = _timeout,
            },
        };

        var endpoint = new EndpointAddress(_endpointAddress);
        var factory = new ChannelFactory<TChannel>(binding, endpoint);
        if (_username is not null && factory.Credentials is not null)
        {
            factory.Credentials.UserName.UserName = _username;
            factory.Credentials.UserName.Password = _password;
        }

        return factory.CreateChannel();
    }
}

public enum SoapTransport { Http, Https }
