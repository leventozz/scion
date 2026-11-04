namespace Scion.Infrastructure.ProjectRegistry;

internal sealed class RegistryDocument
{
    public List<RegistryEntry> Projects { get; set; } = [];
}

internal sealed class RegistryEntry
{
    public string Path { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public DateTimeOffset LastOpenedAt { get; set; }
}
