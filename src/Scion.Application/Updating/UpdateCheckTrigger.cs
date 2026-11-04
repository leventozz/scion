namespace Scion.Application.Updating;

/// <summary>Who asks for the check: Scion itself when it starts, or the user.</summary>
public enum UpdateCheckTrigger
{
    Startup,
    Manual,
}
