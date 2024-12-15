using XmppSharp.Protocol.Base;

namespace Emulator.Net.Model;

public readonly struct XmppStanzaRequestBuilder
{
    public Stanza Element { get; init; }
    public TimeSpan? Timeout { get; init; }
    public TaskCompletionSource Completion { get; init; }
}
