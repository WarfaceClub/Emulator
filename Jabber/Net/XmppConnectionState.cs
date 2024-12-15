namespace Jabber.Net;

public enum XmppConnectionState
{
    None,
    Authenticated = 1 << 0,
    Encrypted = 1 << 1,
    ResourceBinded = 1 << 2
}
