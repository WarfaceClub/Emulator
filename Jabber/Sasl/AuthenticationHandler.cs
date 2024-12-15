using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Jabber.Net;
using XmppSharp;
using XmppSharp.Dom;
using XmppSharp.Protocol.Core.Sasl;

namespace Jabber.Sasl;

public abstract class AuthenticationHandler
{
    public static IReadOnlyDictionary<string, AuthenticationHandler> SupportedMechanisms { get; } = new Dictionary<string, AuthenticationHandler>()
    {
        ["PLAIN"] = new PlainAuthenticationHandler()
    };

    public virtual Task Invoke(IXmppServerConnection connection, Element element)
        => Task.CompletedTask;

    public static void Success(IXmppServerConnection connection, string username)
    {
        var connectionPal = (XmppServerConnection)connection;
        connectionPal._state |= XmppConnectionState.Authenticated;
        connectionPal.Jid = new Jid(username, connectionPal._server.Options.Domain, null);
        connectionPal._parser.Reset();
        connectionPal.Send(new Success());
    }

    [DoesNotReturn]
    public static object Failure(FailureCondition condition, string text = default)
        => throw new JabberSaslException(element: new(condition, text));
}