using Scion.Application.Adapting;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.UI;

internal enum AdaptStep
{
    Form,
    Running,
    Review,
    Failed,
}

/// <summary>
/// Where the "Adapt with AI" dialog is and what the user has entered: the target, the extra instructions, the prepared
/// request, and later the adapted skill with the user's draft of it. A plain class: the dialog asks the use cases and
/// tells this what came back.
/// </summary>
internal sealed class AdaptFlow(Agent sourceAgent)
{
    public AdaptStep Step { get; private set; } = AdaptStep.Form;

    private Dictionary<AiProviderId, AiProviderState> _providerStates = [];

    /// <summary>The state of every provider, as the check at the start found it (and as later events changed it).</summary>
    public IReadOnlyDictionary<AiProviderId, AiProviderState> ProviderStates => _providerStates;

    /// <summary>
    /// The provider that does the adapting. It is chosen apart from the target: choosing it never changes
    /// <see cref="TargetAgent"/>, <see cref="TargetPlace"/> or <see cref="Request"/>. Null until the first check has finished.
    /// </summary>
    public AiProviderId? ChosenProvider { get; private set; }

    /// <summary>The state of the chosen provider; null until the first check has finished.</summary>
    public AiProviderState? Provider =>
        ChosenProvider is { } chosen && _providerStates.TryGetValue(chosen, out var state) ? state : null;

    /// <summary>How the provider's client says it is signed in; <see cref="AiUsageBasis.Unknown"/> until it has said.</summary>
    public AiUsageBasis UsageBasis { get; private set; } = AiUsageBasis.Unknown;

    public bool WaitingForSignIn { get; private set; }

    public CopyPlace? TargetPlace { get; private set; }

    public Agent TargetAgent { get; private set; } = sourceAgent;

    public string ExtraInstructions { get; private set; } = "";

    /// <summary>What is shown and what is sent: it already carries the extra instructions.</summary>
    public AdaptationRequest? Request { get; private set; }

    public PrepareAdaptationError? TargetProblem { get; private set; }

    public AdaptedSkill? Adapted { get; private set; }

    public string Draft { get; private set; } = "";

    public bool Editing { get; private set; }

    /// <summary>The failure of the failed step; the ones that send the user back to the form are never kept here.</summary>
    public AdaptationFailure? Failure { get; private set; }

    public SaveAdaptedSkillError? SaveProblem { get; private set; }

    /// <summary>The skill file as far as the provider has written it while the adaptation runs. For display only: never the result.</summary>
    public string StreamedText { get; private set; } = "";

    /// <summary>When the running adaptation began; the dialog shows how long ago.</summary>
    public DateTimeOffset StartedAt { get; private set; }

    public bool CanAdapt => Step == AdaptStep.Form && Provider == AiProviderState.Ready && !WaitingForSignIn && Request is not null;

    public string SaveLabel => (Adapted?.ExistsInTarget ?? false) ? $"Overwrite in {TargetPlace?.Name}" : $"Save to {TargetPlace?.Name}";

    /// <summary>Only an explicit overwrite replaces a skill the target has; the button says so.</summary>
    public ConflictResolution Resolution => (Adapted?.ExistsInTarget ?? false) ? ConflictResolution.Overwrite : ConflictResolution.Skip;

    public void ProvidersChecked(IReadOnlyDictionary<AiProviderId, AiProviderState> states, AiProviderId chosen)
    {
        _providerStates = new Dictionary<AiProviderId, AiProviderState>(states);
        ChosenProvider = chosen;
    }

    /// <summary>
    /// Only before an adaptation has started and while no sign-in is awaited. The target is left as it is: the provider is
    /// who adapts, the target is where the skill goes.
    /// </summary>
    public void ChooseProvider(AiProviderId provider)
    {
        if (Step != AdaptStep.Form || WaitingForSignIn || provider == ChosenProvider)
        {
            return;
        }

        ChosenProvider = provider;
        UsageBasis = AiUsageBasis.Unknown;
    }

    /// <summary>How the provider says it is paid for; an answer for a provider that is no longer the chosen one is dropped.</summary>
    public void UsageBasisChecked(AiProviderId provider, AiUsageBasis basis)
    {
        if (provider == ChosenProvider)
        {
            UsageBasis = basis;
        }
    }

    public void SignInStarted() => WaitingForSignIn = true;

    public void SignInEnded(AiProviderState state)
    {
        WaitingForSignIn = false;
        SetStateOfChosenProvider(state);
    }

    public void ChooseTarget(CopyPlace place)
    {
        TargetPlace = place;
        ForgetRequest();
    }

    public void ChooseAgent(Agent agent)
    {
        TargetAgent = agent;
        ForgetRequest();
    }

    public void Prepared(Result<AdaptationRequest, PrepareAdaptationError> result)
    {
        Request = result.IsSuccess ? result.Value.WithExtraInstructions(ExtraInstructions) : null;
        TargetProblem = result.IsSuccess ? null : result.Error;
    }

    public void EditInstructions(string text)
    {
        ExtraInstructions = text;
        Request = Request?.WithExtraInstructions(text);
    }

    public void StartAdapting(DateTimeOffset now)
    {
        Step = AdaptStep.Running;
        Failure = null;
        StreamedText = "";
        StartedAt = now;
    }

    /// <summary>What the provider reports while it works. Only a running adaptation shows it; a late report is dropped.</summary>
    public void Progressed(AdaptationProgress progress)
    {
        if (Step != AdaptStep.Running)
        {
            return;
        }

        StreamedText = progress switch
        {
            AdaptationProgress.TextArrived arrived => StreamedText + arrived.Text,
            AdaptationProgress.StartedOver => "",
            _ => throw new ArgumentOutOfRangeException(nameof(progress), progress, null),
        };
    }

    public void AdaptationFinished(Result<AdaptedSkill, AdaptationFailure> result)
    {
        StreamedText = "";

        if (result.IsSuccess)
        {
            Adapted = result.Value;
            Draft = result.Value.Text;
            Editing = false;
            SaveProblem = null;
            Step = AdaptStep.Review;
            return;
        }

        // Cancelling is the user's own choice, and a provider that stopped working is a state of the form, not an error.
        switch (result.Error)
        {
            case AdaptationFailure.NotInstalled:
                SetStateOfChosenProvider(AiProviderState.NotInstalled);
                Step = AdaptStep.Form;
                break;
            case AdaptationFailure.SignedOut:
                SetStateOfChosenProvider(AiProviderState.SignedOut);
                Step = AdaptStep.Form;
                break;
            case AdaptationFailure.Cancelled:
                Step = AdaptStep.Form;
                break;
            default:
                Failure = result.Error;
                Step = AdaptStep.Failed;
                break;
        }
    }

    public void TryAgain()
    {
        Step = AdaptStep.Form;
        Adapted = null;
        Failure = null;
        Draft = "";
        Editing = false;
        SaveProblem = null;
    }

    public void EditBeforeSaving() => Editing = true;

    public void EditDraft(string text) => Draft = text;

    public void SaveRefused(SaveAdaptedSkillError error)
    {
        if (error == SaveAdaptedSkillError.AlreadyExists)
        {
            Adapted = Adapted! with { ExistsInTarget = true };
            SaveProblem = null;
            return;
        }

        SaveProblem = error;
    }

    private void SetStateOfChosenProvider(AiProviderState state)
    {
        if (ChosenProvider is { } chosen)
        {
            _providerStates[chosen] = state;
        }
    }

    private void ForgetRequest()
    {
        Request = null;
        TargetProblem = null;
    }
}
