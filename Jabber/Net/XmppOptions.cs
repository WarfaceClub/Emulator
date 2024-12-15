using System;
using System.Collections.Generic;

namespace Jabber.Net;

public class XmppOptions
{
    public string Domain { get; set; }
    public string Address { get; set; }
    public ushort Port { get; set; }
    public int RecvBufferSize { get; set; }
    public IEnumerable<LocalUserInfo> Users { get; set; }
    public TimeSpan? ThrottleTimeout { get; set; }
    public TimeSpan DisconnectTimeout { get; set; }
    public TimeSpan InactivityTimeout { get; set; }
    public TimeSpan KeepAliveTimeout { get; set; }
    public TimeSpan KeepAliveInterval { get; set; }
    public ResourceConflictStrategy ResourceConflictStrategy { get; set; }

    public static XmppOptions Default { get; } = new()
    {
        Domain = "localhost",
        Address = "127.0.0.1",
        Port = 5222,
        RecvBufferSize = 4096,
        Users =
        [
            new("masterserver", "youshallnotpass"),
            new("dedicated", "youshallnotpass"),
        ],
        ThrottleTimeout = TimeSpan.Zero,
        DisconnectTimeout = TimeSpan.FromSeconds(3),
        InactivityTimeout = TimeSpan.FromSeconds(120),
        KeepAliveTimeout = TimeSpan.FromSeconds(5),
        KeepAliveInterval = TimeSpan.FromSeconds(30),
        ResourceConflictStrategy = ResourceConflictStrategy.KickCurrent
    };
}

public enum ResourceConflictStrategy
{
    KickCurrent,
    KickOther,
    Random,
}

public class LocalUserInfo
{
    public LocalUserInfo()
    {

    }

    public LocalUserInfo(string login, string password)
    {
        Login = login;
        Password = password;
    }

    public string Login { get; set; }
    public string Password { get; set; }
}