namespace Emulator.Net;

public class XmppSession(IXmppServerConnection connection) : IDisposable
{
    public IXmppServerConnection Connection { get; private set; } = connection;

    public event Action<XmppSession> Disposed;

    volatile bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        Disposed?.Invoke(this);

        Connection = null;
    }
}
