using Scion.Application.Adapting;
using Scion.Infrastructure.LocalClients;

namespace Scion.Infrastructure.ClaudeCode;

/// <summary>
/// Asks Claude Code whether it is signed in, how, and starts its sign-in. Whether it is signed in comes from the exit code
/// of <c>claude auth status</c> alone (0 signed in, 1 signed out) and that call's output is dropped. How it is signed in
/// is asked with a second <c>auth status</c> call, and of its JSON only <c>authMethod</c> is read: account, organization
/// and the rest are never looked at, stored or logged. Scion itself reads no environment variable and no credential file
/// for this. The sign-in is Claude Code's own: it opens the browser, and Scion never sees a credential.
/// </summary>
public sealed class ClaudeCodeStatus(ClaudeCodeExecutable executable) : IAiProviderStatus, IAiUsageBasisReader
{
    public async Task<AiProviderState> GetStateAsync(CancellationToken cancellationToken)
    {
        if (executable.Locate() is not { } path)
        {
            return AiProviderState.NotInstalled;
        }

        var outcome = await LocalClientProcess.RunAsync(path, ["auth", "status"], null, OutputHandling.Drop, cancellationToken);

        return outcome switch
        {
            null => AiProviderState.NotInstalled,
            { ExitCode: 0 } => AiProviderState.Ready,
            _ => AiProviderState.SignedOut,
        };
    }

    public async Task<AiUsageBasis> ReadAsync(CancellationToken cancellationToken)
    {
        if (executable.Locate() is not { } path)
        {
            return AiUsageBasis.Unknown;
        }

        var outcome = await LocalClientProcess.RunAsync(path, ["auth", "status"], null, OutputHandling.Keep, cancellationToken);

        return outcome is { ExitCode: 0 } ? ClaudeCodeAuthMethod.UsageBasisIn(outcome.StandardOutput) : AiUsageBasis.Unknown;
    }

    public async Task StartSignInAsync(CancellationToken cancellationToken)
    {
        if (executable.Locate() is not { } path)
        {
            return;
        }

        try
        {
            await LocalClientProcess.RunAsync(path, ["auth", "login"], null, OutputHandling.Drop, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The process has been ended: that is what cancelling a sign-in means.
        }
    }
}
