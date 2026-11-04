namespace Scion.Infrastructure.LocalClients;

/// <param name="StandardOutput">Empty when the output was not kept: it was dropped, or handed on line by line.</param>
public sealed record ProcessOutcome(int ExitCode, string StandardOutput, string StandardError);
