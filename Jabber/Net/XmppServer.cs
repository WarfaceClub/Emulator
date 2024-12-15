using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XmppSharp;
using XmppSharp.Protocol.Base;

namespace Jabber.Net;

public interface IXmppServer
{
    XmppOptions Options { get; }

    IReadOnlyList<IXmppServerConnection> Connections { get; }
    IEnumerable<IXmppServerConnection> FindConnections(Func<IXmppServerConnection, bool> predicate);
    IXmppServerConnection FindConnection(Func<IXmppServerConnection, bool> predicate);

    event Action<IXmppServerConnection> OnConnection;
}

public class XmppServer(
    XmppOptions options,
    ILogger<XmppServer> logger
) : IHostedService, IXmppServer
{
    public XmppOptions Options { get; } = options;

    private Socket _socket;
    private IPEndPoint _endpoint;
    private ConcurrentDictionary<IPAddress, DateTimeOffset> _thorttle = [];
    private List<XmppServerConnection> _connections;

    public event Action<IXmppServerConnection> OnConnection;

    public IReadOnlyList<IXmppServerConnection> Connections
    {
        get
        {
            lock (_connections)
                return [.. _connections];
        }
    }

    public IEnumerable<IXmppServerConnection> FindConnections(Func<IXmppServerConnection, bool> predicate)
    {
        lock (_connections)
        {
            foreach (var entry in _connections)
            {
                if (predicate(entry))
                    yield return entry;
            }
        }
    }

    public IXmppServerConnection FindConnection(Func<IXmppServerConnection, bool> predicate)
    {
        IXmppServerConnection result = null;

        lock (_connections)
        {
            foreach (var entry in _connections)
            {
                if (predicate(entry))
                {
                    result = entry;
                    break;
                }
            }
        }

        return result;
    }

    async Task IHostedService.StartAsync(CancellationToken token)
    {
        await Task.Yield();

        _connections = [];

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        if (!IPAddress.TryParse(Options.Address, out var address))
        {
            logger.LogWarning("Cannot parse '{Text}' as well-formed IP address.", Options.Address);
            address = IPAddress.Loopback;
        }

        _endpoint = new IPEndPoint(address, Options.Port);
        _socket.Bind(_endpoint);
        _socket.Listen();

        logger.LogInformation("Starting XMPP server. (endpoint: {EndPoint})", _endpoint);

        _ = Task.Run(() => BeginAccept(token), token);
    }

    async Task BeginAccept(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(1);

            try
            {
                var client = await _socket.AcceptAsync(token);
                _ = Task.Run(() => EndAccept(client), token);
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    logger.LogDebug(ex, "Socket accept failed");
            }
        }
    }

    protected virtual string GenerateId() => Guid.NewGuid().ToString("D");

    async Task EndAccept(Socket s)
    {
        var sessionId = GenerateId();

        var remoteAddress = (s.RemoteEndPoint as IPEndPoint).Address;

        if (_thorttle.TryGetValue(remoteAddress, out var expirationTime))
        {
            if (DateTimeOffset.UtcNow <= expirationTime)
            {
                var timeWait = expirationTime.Subtract(DateTimeOffset.UtcNow);

                using (var stream = new NetworkStream(s, true))
                {
                    var element = new StreamStream
                    {
                        From = Options.Domain,
                        Id = sessionId,
                    };

                    element.AddChild(new StreamError
                    {
                        Condition = StreamErrorCondition.NotAuthorized,
                        Text = $"Connection throttle - Wait {timeWait.TotalSeconds:F2} second(s) before connecting again.",
                    });

                    await stream.WriteAsync(element.ToString().GetBytes());

                    return;
                }
            }
        }

        // If throttle time > 0, then is enabled on the server.
        if (Options.ThrottleTimeout.HasValue)
            _thorttle[remoteAddress] = DateTimeOffset.UtcNow.Add(Options.ThrottleTimeout.Value);

        using var connection = new XmppServerConnection
        {
            Id = sessionId,
            RemoteAddress = remoteAddress,
            _server = this,
            _socket = s,
            _logger = logger,
            _state = XmppConnectionState.None
        };

        lock (_connections)
            _connections.Add(connection);

        try
        {
            OnConnection?.Invoke(connection);

            await connection.StartAsync();
        }
        catch (Exception ex)
        {
            logger.LogTrace(ex, "An error occurred while processing the connection {StreamId}", connection.Id);
        }
        finally
        {
            lock (_connections)
                _connections.Remove(connection);

            _thorttle.TryRemove(remoteAddress, out _);
        }
    }

    async Task IHostedService.StopAsync(CancellationToken token)
    {
        logger.LogInformation("Stopping xmpp server {EndPoint}", _endpoint);

        XmppServerConnection[] temp;

        lock (_connections)
        {
            temp = _connections.ToArray();
            _connections.Clear();
        }

        var element = new StreamError(StreamErrorCondition.SystemShutdown);

        foreach (var connection in temp)
            connection.Disconnect(element);

        var delayTask = Task.Delay(Options.DisconnectTimeout);

        while (true)
        {
            if (delayTask.IsCompleted)
                break;

            if (temp.All(x => !x.IsConnected))
                break;

            await Task.Delay(160);
        }

        _socket.Dispose();
        _socket = null;
    }
}