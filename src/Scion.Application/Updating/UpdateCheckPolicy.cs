namespace Scion.Application.Updating;

/// <summary>How often Scion may ask on its own. A check the user asks for is not held to it.</summary>
public static class UpdateCheckPolicy
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);
}
