using XmppSharp.Dom;
using XmppSharp.Protocol.Base;
using XmppSharp.Protocol.Core.Sasl;

namespace Emulator.Net.Model;

public class XmppDisconnectBuilder
{
    public Element Element { get; set; }

    public FailureCondition? Failure
    {
        get => (Element as Failure).Condition;
        set
        {
            Element = null;

            if (value.HasValue)
                Element = new Failure(value);
        }
    }

    public StreamErrorCondition? StreamError
    {
        get => (Element as StreamError).Condition;
        set
        {
            Element = null;

            if (value.HasValue)
                Element = new StreamError(value);
        }
    }
}