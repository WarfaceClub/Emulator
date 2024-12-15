namespace Emulator.Net.Model;

public readonly struct XmppPacket
{
    public string Xml { get; init; }
    public byte[] Bytes { get; init; }
    public TaskCompletionSource Completion { get; init; }
}
