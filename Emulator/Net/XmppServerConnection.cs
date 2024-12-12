using System.Net.Sockets;

namespace Emulator.Net;

public interface IXmppServerConnection
{
    string SessionId { get; }
}

public class XmppServerConnection : IXmppServerConnection, IDisposable
{
    public string SessionId { get; } = Guid.NewGuid().ToString("d");

    internal XmppServer _server;
    internal Socket _socket;
    internal Stream _stream;

    public XmppServerConnection(XmppServer server, Socket socket)
    {
        _server = server;
        _stream = new NetworkStream(_socket = socket, false);
    }

    internal async Task StartAsync()
    {
        await Task.Yield();
    }

    public void Dispose()
    {

    }
}