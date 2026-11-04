namespace Scion.Domain;

public sealed record RegisteredProject(string Path, string DisplayName, DateTimeOffset LastOpenedAt)
{
    public RegisteredProject Open(DateTimeOffset now) => this with { LastOpenedAt = now };

    /// <summary>Trims the ends of the name and keeps inner spaces. A name that is empty after trimming is rejected.</summary>
    public Result<RegisteredProject, DisplayNameError> Rename(string displayName)
    {
        var trimmed = displayName.Trim();

        return trimmed.Length == 0
            ? Result<RegisteredProject, DisplayNameError>.Failure(new DisplayNameError.Empty())
            : Result<RegisteredProject, DisplayNameError>.Success(this with { DisplayName = trimmed });
    }
}
