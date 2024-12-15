using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Emulator.Net.Model;
using Serilog;
using XmppSharp;
using XmppSharp.Dom;
using XmppSharp.Expat;
using XmppSharp.Parser;
using XmppSharp.Protocol.Base;
using XmppSharp.Protocol.Core.Tls;

namespace Emulator.Net;

public enum XmppConnectionState
{
    None,
    Connected = 1 << 0,
    Encrypted = 1 << 1,
    Authenticated = 1 << 2,
    ResourceBinded = 1 << 3,
    SessionStarted = 1 << 4
}

public interface IXmppServerConnection
{
    string SessionId { get; }
    Jid Jid { get; }
    IPAddress RemoteAddress { get; }
    XmppConnectionState State { get; }
    bool IsConnected { get; }
    XmppSession Session { get; }

    void Send(Element e);
    Task SendAsync(Element e);

    void Disconnect(Action<XmppDisconnectBuilder> builder);

    event Action<IXmppServerConnection> OnConnect;
    event Action<IXmppServerConnection, Stanza> OnStanza;
    event Action<IXmppServerConnection, Element> OnElement;
    event Action<IXmppServerConnection> OnDisconnect;
}

public class XmppServerConnection : IXmppServerConnection, IDisposable
{
    public event Action<IXmppServerConnection> OnConnect;
    public event Action<IXmppServerConnection, Stanza> OnStanza;
    public event Action<IXmppServerConnection, Element> OnElement;
    public event Action<IXmppServerConnection> OnDisconnect;

    public string SessionId { get; } = Guid.NewGuid().ToString("d");
    public Jid Jid { get; private set; }
    public IPAddress RemoteAddress { get; }

    public XmppConnectionState State => _state;
    public bool IsConnected => _state.HasFlag(XmppConnectionState.Connected) && !_disposed.HasFlag(FileAccess.Read);

    internal XmppServer _server;
    internal Socket _socket;
    internal Stream _stream;
    internal volatile FileAccess _paused;
    internal volatile XmppConnectionState _state;
    internal volatile FileAccess _disposed;
    internal XmppSession _session;
    internal X509Certificate2 _certificate;

    public XmppSession Session => _session;

    private ExpatXmppParser _parser;
    private ConcurrentQueue<XmppPacket> _sendQueue;

    public XmppServerConnection(XmppServer server, Socket socket)
    {
        _server = server;
        _stream = new NetworkStream(_socket = socket, false);
        _sendQueue = [];
        _session = new(this);

        RemoteAddress = ((IPEndPoint)_socket.RemoteEndPoint).Address;

        InitParser();
    }

    void InitParser()
    {
        _parser = new ExpatXmppParser(ExpatEncoding.UTF8);

        _parser.OnStreamStart += e =>
        {
            Log.Debug("<{Client}> recv <<\n{Xml}\n", RemoteAddress, e.StartTag());

            var hostname = e.To;

            e.SwitchDirection();
            e.From = _server._options.Hostname;
            Send(e.StartTag());

            if (hostname != _server._options.Hostname)
            {
                Disconnect(x => x.StreamError = StreamErrorCondition.HostUnknown);
                return;
            }

            var features = new StreamFeatures();

            if (!_state.HasFlag(XmppConnectionState.Authenticated))
            {
                if (!_state.HasFlag(XmppConnectionState.Encrypted))
                    features.StartTls = new(StartTlsPolicy.Required);

                features.Mechanisms = new();
                features.Mechanisms.AddMechanism("WARFACE");
            }
            else
            {
                if (!_state.HasFlag(XmppConnectionState.ResourceBinded))
                    features.SupportBind = true;

                if (!_state.HasFlag(XmppConnectionState.SessionStarted))
                    features.SupportSession = true;
            }

            Send(features);
        };

        _parser.OnStreamElement += e =>
        {
            Log.Debug("<{Client}> recv <<\n{Xml}\n", RemoteAddress, e.ToString(true));

            try
            {
                if (e is Stanza stz)
                {
                    OnStanza?.Invoke(this, stz);
                    return;
                }
                else
                {
                    OnElement?.Invoke(this, e);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);

                Disconnect(x => x.StreamError = StreamErrorCondition.InternalServerError);
            }
        };

        _parser.OnStreamEnd += () =>
        {
            Log.Debug("<{Client}> recv <<\n{Xml}\n", RemoteAddress, Xml.XmppStreamEnd);
            Disconnect();
        };
    }

    internal async Task StartAsync()
    {
        _paused = 0;
        _state = XmppConnectionState.Connected;
        await Task.WhenAll(BeginReceive(), BeginSend());
    }

    async Task BeginReceive()
    {
        var length = Math.Clamp(_server._options.RecvBufferSize, 1024, 9216);
        var buffer = ArrayPool<byte>.Shared.Rent(length);

        try
        {
            while (!_disposed.HasFlag(FileAccess.Read))
            {
                await Task.Delay(1);

                if (_paused.HasFlag(FileAccess.Read))
                    continue;

                length = await _stream.ReadAsync(buffer);

                if (length <= 0)
                    break;

                _parser.Write(buffer, length);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            Disconnect(x => x.StreamError = StreamErrorCondition.InternalServerError);
        }
        finally
        {
            Disconnect();
        }

        ArrayPool<byte>.Shared.Return(buffer, true);
    }

    async Task BeginSend()
    {
        try
        {
            while (!_disposed.HasFlag(FileAccess.Write))
            {
                await Task.Delay(1);

                if (_paused.HasFlag(FileAccess.Write))
                    continue;

                while (_sendQueue.TryDequeue(out var packet))
                {
                    await Task.Delay(1);

                    try
                    {
                        if (packet.Bytes != null)
                            await _stream.WriteAsync(packet.Bytes);

#if DEBUG
                        Log.Debug("<{Client}> send >>\n{Xml}\n", RemoteAddress, packet.Xml);
#endif
                    }
                    catch (Exception ex)
                    {
                        packet.Completion?.TrySetException(ex);
                        return;
                    }
                    finally
                    {
                        packet.Completion?.TrySetResult();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        finally
        {
            Dispose();
        }
    }

    internal void Send(string xml)
    {
        if (_disposed.HasFlag(FileAccess.Write))
            return;

        _sendQueue.Enqueue(new XmppPacket
        {
#if DEBUG
            Xml = xml,
#endif
            Bytes = xml.GetBytes(),
        });
    }

    public void Send(Element e)
    {
        if (_disposed.HasFlag(FileAccess.Write))
            return;

        _sendQueue.Enqueue(new XmppPacket
        {
#if DEBUG
            Xml = e.ToString(true),
#endif
            Bytes = e.GetBytes(),
        });
    }

    public async Task SendAsync(Element e)
    {
        if (_disposed.HasFlag(FileAccess.Write))
            return;

        var tcs = new TaskCompletionSource();

        _sendQueue.Enqueue(new XmppPacket
        {
#if DEBUG
            Xml = e.ToString(true),
#endif
            Bytes = e.GetBytes(),
            Completion = tcs
        });

        await tcs.Task;
    }


    // Ensure will call Disconnect once.
    volatile bool _beginDisconnect = false;

    public void Disconnect(Action<XmppDisconnectBuilder> builder = default)
    {
        if (_beginDisconnect) // Disconnect packet already queued.
            return;

        _beginDisconnect = true;

        if (!_state.HasFlag(XmppConnectionState.Connected)) // Disposed called before.
            return;

        var sb = new StringBuilder();

        if (builder != null)
        {
            var packet = new XmppDisconnectBuilder();

            builder(packet);

            if (packet.Element != null)
                sb.Append(packet.Element.ToString(false));
        }

        // Queue the packet as raw string.
        Send(sb.Append(Xml.XmppStreamEnd).ToString());

        Dispose();
    }

    public void Dispose()
    {
        if (_disposed > 0)
            return;

        _disposed |= FileAccess.Read;
        _paused |= FileAccess.Read;
        _state &= ~XmppConnectionState.Connected;
        _socket.Shutdown(SocketShutdown.Receive);

        if (_sendQueue.IsEmpty)
            Cleanup();
        else
        {
            _ = Task.Run(async () =>
            {
                var timeout = Task.Delay(TimeSpan.FromSeconds(3));

                while (true)
                {
                    if (timeout.IsCompleted)
                        break;

                    if (_sendQueue.IsEmpty)
                        break;

                    await Task.Delay(16);
                }

                Cleanup();
            });
        }

        GC.SuppressFinalize(this);
    }

    void Cleanup()
    {
        _paused |= FileAccess.Write;
        _disposed |= FileAccess.Write;

        _sendQueue.Clear();
        _stream.Dispose();
        _certificate?.Dispose();
        _parser.Dispose();
        _socket.Shutdown(SocketShutdown.Send);
        _socket.Dispose();

        _socket = null;
        _sendQueue = null;
        _certificate = null;
        _parser = null;
        _socket = null;
        _session = null;
    }
}