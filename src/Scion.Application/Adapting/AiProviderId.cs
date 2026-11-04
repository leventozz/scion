namespace Scion.Application.Adapting;

/// <summary>Tells one <see cref="AiProvider"/> from another. It is the key the provider's ports are found by.</summary>
public readonly record struct AiProviderId
{
    public AiProviderId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
}
