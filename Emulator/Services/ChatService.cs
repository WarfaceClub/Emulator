using Emulator.Abstractions;
using Emulator.Attributes;
using Emulator.Entities.Options;
using Emulator.Net;
using Microsoft.Extensions.Options;
using XmppSharp.Protocol.Base;
using XmppSharp.Protocol.Core;

namespace Emulator.Services;

[Bootstrap]
public class ChatService : IBootstrap
{
    private IXmppServer _server;
    private ChatOptions _options;

    public ChatService(IOptions<ChatOptions> options, IXmppServer server)
    {
        _options = options.Value;
        _server = server;
        _server.NewConnection += OnNewConnection;
    }

    void OnNewConnection(IXmppServerConnection connection)
    {
        connection.OnStanza += HandleStanza;
        connection.OnDisconnect += HandleDisconnected;
    }

    void HandleStanza(IXmppServerConnection connection, Stanza stz)
    {
        if (stz is Message message)
        {

        }
        else if (stz is Presence presence)
        {

        }
    }

    void HandleDisconnected(IXmppServerConnection connection)
    {
        connection.OnStanza -= HandleStanza;
        connection.OnDisconnect -= HandleDisconnected;
    }
}
