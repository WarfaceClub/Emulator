using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Jabber.Sasl;
using Microsoft.Extensions.Logging;
using XmppSharp;
using XmppSharp.Dom;
using XmppSharp.Expat;
using XmppSharp.Parser;
using XmppSharp.Protocol.Base;
using XmppSharp.Protocol.Core.Sasl;
using XmppSharp.Protocol.Core.Tls;

namespace Jabber.Net;

public interface IXmppServerConnection
{
    IPAddress RemoteAddress { get; }
    string Id { get; }
    Jid Jid { get; }
    IXmppServer Server { get; }

    bool IsConnected { get; }
    bool IsAuthenticated { get; }
    XmppConnectionState State { get; }

    void Send(Element element);
    Task SendAsync(Element element);
    void Disconnect(Element element = default);

    event Action<IXmppServerConnection> OnDisconnect;
    public event Action<Element> OnAuthentication;
}

public readonly struct XmppSendPacket
{
    public string DebugXml { get; init; }
    public byte[] Payload { get; init; }
    public TaskCompletionSource Completion { get; init; }
}

public class XmppServerConnection : IXmppServerConnection, IDisposable
{
    public event Action<IXmppServerConnection> OnDisconnect;
    public event Action<Element> OnAuthentication;

    public string Id { get; internal init; }
    public IPAddress RemoteAddress { get; init; }

    public Jid Jid { get; internal set; }
    public bool IsAuthenticated => _state.HasFlag(XmppConnectionState.Authenticated);
    public bool IsConnected => _state > 0;

    internal XmppServer _server;
    internal Stream _stream;
    internal Socket _socket;
    internal ExpatXmppParser _parser;
    internal XmppConnectionState _state;
    internal ILogger _logger;

    private ConcurrentQueue<XmppSendPacket> _sendQueue;

    IXmppServer IXmppServerConnection.Server => _server;
    XmppConnectionState IXmppServerConnection.State => _state;

    private volatile NetworkState _networkState;

    [Flags]
    enum NetworkState
    {
        None = 0,
        CancelWrite = 1 << 0,
        CancelRead = 1 << 1,
        SuspendRead = 1 << 2,
        SuspendWrite = 1 << 3
    }

    void InitParser()
    {
        _parser = new ExpatXmppParser(ExpatEncoding.UTF8);

        _parser.OnStreamStart += (e) =>
        {
            if (Utilities.IsDevBuild)
                _logger.LogDebug("recv <<\n{Xml}\n", e.StartTag());

            SendStreamHeader();

            if (e.To != _server.Options.Domain)
            {
                Disconnect(new StreamError(StreamErrorCondition.HostUnknown));
                return;
            }

            var features = new StreamFeatures();

            if (!IsAuthenticated)
            {
                features.Mechanisms = new()
                {
                    SupportedMechanisms = AuthenticationHandler
                        .SupportedMechanisms
                        .Select(x => new Mechanism(x.Key))
                };

                // if (_stream is not SslStream)
                //     features.StartTls = new(StartTlsPolicy.Optional);
            }
            else
            {
                features.SupportBind = true;
                features.SupportSession = true;
            }

            Send(features);
        };

        _parser.OnStreamElement += e =>
        {
            if (Utilities.IsDevBuild)
                _logger.LogDebug("recv <<\n{Xml}\n", e.ToString(true));

            try
            {
                AsyncHelper.RunSync(() => OnElement(e));
            }
            catch (Exception ex)
            {
                _logger.LogError(message: ex.ToString());
            }
        };

        _parser.OnStreamEnd += () =>
        {
            if (Utilities.IsDevBuild)
                _logger.LogDebug("recv <<\n{Xml}\n", Xml.XmppStreamEnd);

            Disconnect();
        };
    }

    internal async Task StartAsync()
    {
        _networkState = 0;

        _sendQueue = [];
        _stream = new NetworkStream(_socket, false);

        InitParser();

        await Task.WhenAll(BeginReceive(), BeginSend());

        Disconnect();
    }

    async Task BeginReceive()
    {
        var buf = new byte[Math.Clamp(_server.Options.RecvBufferSize, 1024, 9261)];

        try
        {
            while (!_networkState.HasFlag(NetworkState.CancelRead))
            {
                await Task.Delay(1);

                if (_networkState.HasFlag(NetworkState.SuspendRead))
                    continue;

                int len = await _stream.ReadAsync(buf);

                _parser?.Write(buf, len);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Recv failed");

            Disconnect(new StreamError(StreamErrorCondition.InternalServerError)
            {
                Text = ex.Message
            });
        }
    }

    async Task BeginSend()
    {
        while (!_networkState.HasFlag(NetworkState.CancelWrite))
        {
            await Task.Delay(1);

            if (_networkState.HasFlag(NetworkState.SuspendWrite))
                continue;

            while (_sendQueue.TryDequeue(out var packet))
            {
                try
                {
                    await _stream.WriteAsync(packet.Payload);

                    if (Utilities.IsDevBuild)
                        _logger.LogDebug("send >>\n{Xml}\n", packet.DebugXml);
                }
                catch (Exception ex)
                {
                    packet.Completion?.TrySetException(ex);
                }
                finally
                {
                    packet.Completion?.TrySetResult();
                }
            }
        }
    }

    internal void AddToSendQueue(XmppSendPacket packet)
    {
        if (_networkState.HasFlag(NetworkState.CancelWrite))
            return;

        _sendQueue.Enqueue(packet);
    }

    internal void SendStreamHeader()
    {
        var xml = new StreamStream
        {
            From = _server.Options.Domain,
            Id = Id,
            Version = "1.0",
            Language = "en"
        }.StartTag();

        AddToSendQueue(new()
        {
            DebugXml = Utilities.IsDevBuild ? xml : null,
            Payload = xml.GetBytes()
        });
    }

    public void Send(Element e)
    {
        AddToSendQueue(new()
        {
            DebugXml = Utilities.IsDevBuild ? e.ToString(true) : null,
            Payload = e.ToString(false).GetBytes()
        });
    }

    volatile bool isAuthenticating = false;

    async Task OnElement(Element e)
    {
        await Task.Yield();

    _authStarted:
        if (isAuthenticating)
        {
            if (IsAuthenticated)
            {
                isAuthenticating = false;
                goto _authEnd;
            }

            OnAuthentication?.Invoke(e);
            return;
        }

    _authEnd:

        if (e is StartTls)
        {
            if (_stream is SslStream)
            {
                Disconnect(new StreamError(StreamErrorCondition.UnsupportedFeature));
                return;
            }
            else
            {
                // TODO: Handle starttls.
            }
        }

        if (e is Auth)
        {
            isAuthenticating = true;
            goto _authStarted;
        }
    }

    public Task SendAsync(Element e)
    {
        var tcs = new TaskCompletionSource();

        AddToSendQueue(new()
        {
            DebugXml = Utilities.IsDevBuild ? e.ToString(true) : null,
            Payload = e.ToString(false).GetBytes(),
            Completion = tcs
        });

        return tcs.Task;
    }

    public void Disconnect(Element element = default)
    {
        var sb = new StringBuilder();

        if (element != null)
            sb.Append(element.ToString(false));

        var xml = sb.Append(Xml.XmppStreamEnd).ToString();

        AddToSendQueue(new()
        {
            DebugXml = Utilities.IsDevBuild ? xml : null,
            Payload = xml.GetBytes()
        });

        Dispose();
    }

    public void Dispose()
    {
        if (_networkState > 0)
            return;

        _networkState = NetworkState.SuspendRead | NetworkState.CancelRead;
        _socket.Shutdown(SocketShutdown.Receive);
        _parser.Dispose();

        OnDisconnect?.Invoke(this);

        if (_sendQueue.IsEmpty)
            DisposeCore();
        else
        {
            _ = Task.Delay(_server.Options.DisconnectTimeout)
                .ContinueWith(_ => DisposeCore());
        }
    }

    void DisposeCore()
    {
        _networkState |= NetworkState.CancelWrite | NetworkState.SuspendWrite;

        _stream.Dispose();
        _stream = null;

        _socket.Dispose();
        _socket = null;

        _sendQueue = null;
        _parser = null;
    }
}