namespace Scion.Application.Adapting;

/// <summary>Who adapts a skill, as the user knows it. Not an <c>Agent</c>: that is where a skill lives.</summary>
/// <param name="Id">Tells this provider from the others; the key of its ports.</param>
/// <param name="Name">The provider's name as the user knows it.</param>
/// <param name="ClientName">The program that is reached through (the provider's own command-line client).</param>
/// <param name="SignInWith">What the user signs in with, as the sign-in button says it ("Sign in with ChatGPT").</param>
/// <param name="InstallHelp">Where the user learns how to install the client.</param>
/// <param name="UsageNotes">The line the dialog shows under the provider for each <see cref="AiUsageBasis"/> the client reports.</param>
public sealed record AiProvider(
    AiProviderId Id, string Name, string ClientName, string SignInWith, Uri InstallHelp, AiUsageNotes UsageNotes);
