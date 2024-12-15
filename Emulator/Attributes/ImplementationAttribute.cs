namespace Emulator.Attributes;

public interface IServiceImplementation
{
    public Type ImplementationType { get; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ImplementationAttribute<T> : Attribute, IServiceImplementation
{
    public Type ImplementationType { get; } = typeof(T);
}
