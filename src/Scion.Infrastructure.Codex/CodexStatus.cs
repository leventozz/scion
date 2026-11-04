using Scion.Application.Adapting;
using Scion.Infrastructure.LocalClients;

namespace Scion.Infrastructure.Codex;

/// <summary>
/// Asks Codex whether it is signed in and starts its sign-in. Whether it is signed in comes from the exit code of
/// <c>codex login status</c> alone (0 signed in, anything else signed out) and that call's output is dropped. The sign-in
/// is Codex's own: it opens the browser, and Scion never sees a credential.
/// <para>
/// How a run is paid for is never read: <c>codex exec</c> prefers an API key in the environment to the sign-in, so what the
/// client reports about the sign-in does not tell what a run counts against. The provider's note is a fixed line instead.
/// </para>
/// </summary>
public sealed class CodexStatus(CodexExecutable executable) : IAiProviderStatus, IAiUsageBasisReader
{
    public async Task<AiProviderState> GetStateAsync(CancellationToken cancellationToken)
    {
        if (executable.Locate() is not { } path)
        {
            return AiProviderState.NotInstalled;
        }

        var outcome = await LocalClientProcess.RunAsync(path, ["login", "status"], null, OutputHandling.Drop, cancellationToken);

        return outcome switch
        {
            null => AiProviderState.NotInstalled,
            { ExitCode: 0 } => AiProviderState.Ready,
            _ => AiProviderState.SignedOut,
        };
    }

    public Task<AiUsageBasis> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(AiUsageBasis.Unknown);

    public async Task StartSignInAsync(CancellationToken cancellationToken)
    {
        if (executable.Locate() is not { } path)
        {
            return;
        }

        try
        {
            await LocalClientProcess.RunAsync(path, ["login"], null, OutputHandling.Drop, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The process has been ended: that is what cancelling a sign-in means.
        }
    }
}
