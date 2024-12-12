using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Emulator.Entities.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Emulator.Net;

public interface IXmppServer
{
    IEnumerable<IXmppServerConnection> Connections();
}

public class XmppServer : BackgroundService, IXmppServer
{
    private Socket _socket;
    private ConcurrentDictionary<string, XmppServerConnection> _connections = [];
    private readonly ILogger<XmppServer> _logger;
    internal readonly IServiceProvider _services;
    internal readonly XmppServerOptions _options;

    public XmppServer
    (
        ILogger<XmppServer> logger,
        IServiceProvider services,
        IOptions<XmppServerOptions> options
    )
    {
        _logger = logger;
        _services = services;

        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.Hostname))
            throw new ArgumentException("XMPP hostname cannot be null or empty.");
    }

    public IEnumerable<IXmppServerConnection> Connections() => _connections.Values;

    public override void Dispose()
    {
        base.Dispose();
        _socket.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        var endpoint = new IPEndPoint(
            _options.IsLocal ? IPAddress.Loopback : IPAddress.Any,
            _options.Port
        );

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _socket.Bind(endpoint);
        _socket.Listen(8);

        _logger.LogInformation("Starting xmpp server. tcp://{EndPoint}/{Hostname}", endpoint, _options.Hostname);

        while (!token.IsCancellationRequested)
        {
            await Task.Delay(16);

            try
            {
                var client = await _socket.AcceptAsync(token);

                if (client != null)
                    _ = Task.Run(() => EndAccept(client), token);
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Socket accept failed");
            }
        }
    }

    async Task EndAccept(Socket s)
    {
        using var connection = new XmppServerConnection(this, s);

        _connections[connection.SessionId] = connection;

        try
        {
            await connection.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.ToString());
        }
        finally
        {
            _connections.TryRemove(connection.SessionId, out _);
        }
    }
}