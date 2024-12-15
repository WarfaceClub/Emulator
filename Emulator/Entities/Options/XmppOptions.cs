namespace Emulator.Entities.Options;

public class XmppOptions
{
    public string Hostname { get; set; }
    public ushort Port { get; set; }
    public int RecvBufferSize { get; set; }
    public bool IsLocal { get; set; }
    public IEnumerable<XmppUserInfo> Users { get; set; }

    public class XmppUserInfo
    {
        public string Login { get; set; }
        public string Password { get; set; }
    }
}
