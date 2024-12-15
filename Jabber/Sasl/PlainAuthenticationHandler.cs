using System;
using System.Threading.Tasks;
using Jabber.Net;
using XmppSharp;
using XmppSharp.Dom;
using XmppSharp.Protocol.Core.Sasl;

namespace Jabber.Sasl;

public class PlainAuthenticationHandler : AuthenticationHandler
{
    public override async Task Invoke(IXmppServerConnection connection, Element element)
    {
        await Task.Yield();

        if (element is Auth auth)
        {
            if (auth.Mechanism != "PLAIN")
                Failure(FailureCondition.InvalidMechanism);

            var sasl = Convert.FromBase64String(auth.Value)
                .GetString()
                .Split('\0', StringSplitOptions.TrimEntries);

            if (sasl.Length < 2)
                Failure(FailureCondition.MalformedRequest);

            var ofs = sasl.Length == 3 ? 1 : 0;
            var username = sasl[ofs];
            var password = sasl[ofs + 1];

            foreach (var user in connection.Server.Options.Users)
            {
                if (username == user.Login && password == user.Password)
                {
                    Success(connection, user.Login);
                    return;
                }
            }

            // TODO: Authenticate later routing an query to masterserver@warface
            // iq[@type='set']/query[@xmlns='urn:cryonline:k01']/account[@login='',@password='']
            //   iq type == result, login/pass -> success
            //   iq type == error, login/pass -> failure
            Success(connection, username);
        }
    }
}