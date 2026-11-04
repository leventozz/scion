using Scion.Application.Adapting;
using Scion.Application.Editing;
using Scion.Application.Skills;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.UI.Tests;

public class AdaptFlowTests
{
    private static readonly DateTimeOffset Noon = UiFakes.Noon;
    private static readonly CopyPlace Web = new("Web", new Location.Project("/work/web"));
    private static readonly AgentLocation Source = new(new Location.Project("/work/api"), Agent.ClaudeCode);

    private static AdaptationRequest Request(string extra = "") => new(
        Source, "docker", "docker", "---\nname: docker\n---\n", new TextFormat(false, LineEnding.Lf),
        new AgentLocation(Web.Location, Agent.ClaudeCode), "Web", [], [], extra, []);

    private static AdaptedSkill Adapted(bool exists = false) => new(
        "---\nname: docker\n---\nAdapted\n",
        new SkillSummary("docker", "docker", "d", [], CanBeDeleted: true),
        exists);

    private static readonly AiProviderId Claude = new("claude");
    private static readonly AiProviderId Codex = new("codex");

    /// <summary>The check at the start of the dialog: Claude is the provider the flow begins with.</summary>
    private static void Check(AdaptFlow flow, AiProviderState claude, AiProviderState codex = AiProviderState.Ready) =>
        flow.ProvidersChecked(new Dictionary<AiProviderId, AiProviderState> { [Claude] = claude, [Codex] = codex }, Claude);

    private static Result<T, AdaptationFailure> Fail<T>(AdaptationFailure failure) => Result<T, AdaptationFailure>.Failure(failure);

    private static AdaptFlow ReadyFlow()
    {
        var flow = new AdaptFlow(Agent.ClaudeCode);
        Check(flow, AiProviderState.Ready);
        flow.ChooseTarget(Web);
        flow.Prepared(Result<AdaptationRequest, PrepareAdaptationError>.Success(Request()));

        return flow;
    }

    [Fact]
    public void Starts_on_the_form_with_the_source_agent_and_nothing_chosen()
    {
        var flow = new AdaptFlow(Agent.Codex);

        Assert.Equal(AdaptStep.Form, flow.Step);
        Assert.Equal(Agent.Codex, flow.TargetAgent);
        Assert.Null(flow.TargetPlace);
        Assert.Null(flow.Request);
        Assert.False(flow.CanAdapt);
    }

    [Fact]
    public void Can_adapt_only_when_the_provider_is_ready_and_a_request_is_prepared()
    {
        var flow = ReadyFlow();
        Assert.True(flow.CanAdapt);

        Check(flow, AiProviderState.SignedOut);
        Assert.False(flow.CanAdapt);

        Check(flow, AiProviderState.NotInstalled);
        Assert.False(flow.CanAdapt);

        var checking = new AdaptFlow(Agent.ClaudeCode);
        checking.ChooseTarget(Web);
        checking.Prepared(Result<AdaptationRequest, PrepareAdaptationError>.Success(Request()));
        Assert.False(checking.CanAdapt);
    }

    [Fact]
    public void Cannot_adapt_while_waiting_for_the_sign_in()
    {
        var flow = ReadyFlow();

        flow.SignInStarted();

        Assert.True(flow.WaitingForSignIn);
        Assert.False(flow.CanAdapt);
    }

    [Fact]
    public void Ending_the_sign_in_takes_the_state_it_ended_with()
    {
        var flow = new AdaptFlow(Agent.ClaudeCode);
        Check(flow, AiProviderState.SignedOut);
        flow.SignInStarted();

        flow.SignInEnded(AiProviderState.Ready);

        Assert.False(flow.WaitingForSignIn);
        Assert.Equal(AiProviderState.Ready, flow.Provider);
    }

    [Fact]
    public void Choosing_another_target_drops_the_request_and_the_problem()
    {
        var flow = ReadyFlow();

        flow.ChooseTarget(new CopyPlace("Other", new Location.Project("/work/other")));

        Assert.Null(flow.Request);
        Assert.Null(flow.TargetProblem);
        Assert.False(flow.CanAdapt);
    }

    [Fact]
    public void A_target_that_cannot_be_prepared_keeps_its_problem_and_no_request()
    {
        var flow = new AdaptFlow(Agent.ClaudeCode);
        Check(flow, AiProviderState.Ready);
        flow.ChooseTarget(Web);

        flow.Prepared(Result<AdaptationRequest, PrepareAdaptationError>.Failure(PrepareAdaptationError.TargetFolderMissing));

        Assert.Equal(PrepareAdaptationError.TargetFolderMissing, flow.TargetProblem);
        Assert.Null(flow.Request);
        Assert.False(flow.CanAdapt);
    }

    [Fact]
    public void Extra_instructions_go_into_the_request_that_is_shown_and_sent()
    {
        var flow = ReadyFlow();

        flow.EditInstructions("We use PostgreSQL.");

        Assert.Equal("We use PostgreSQL.", flow.Request!.ExtraInstructions);
        Assert.Contains(flow.Request.SharedItems, item => item.Label == "Your extra instructions");
    }

    [Fact]
    public void Extra_instructions_written_before_the_target_was_chosen_are_kept_for_its_request()
    {
        var flow = new AdaptFlow(Agent.ClaudeCode);
        flow.EditInstructions("We use PostgreSQL.");
        flow.ChooseTarget(Web);

        flow.Prepared(Result<AdaptationRequest, PrepareAdaptationError>.Success(Request()));

        Assert.Equal("We use PostgreSQL.", flow.Request!.ExtraInstructions);
    }

    [Fact]
    public void Starting_moves_to_running_and_clears_an_earlier_failure()
    {
        var flow = ReadyFlow();
        flow.StartAdapting(Noon);
        flow.AdaptationFinished(Fail<AdaptedSkill>(new AdaptationFailure.TimedOut()));

        flow.TryAgain();
        flow.StartAdapting(Noon);

        Assert.Equal(AdaptStep.Running, flow.Step);
        Assert.Null(flow.Failure);
    }

    [Fact]
    public void A_result_moves_to_the_review_with_the_adapted_text_as_the_draft()
    {
        var flow = ReadyFlow();
        flow.StartAdapting(Noon);

        flow.AdaptationFinished(Result<AdaptedSkill, AdaptationFailure>.Success(Adapted()));

        Assert.Equal(AdaptStep.Review, flow.Step);
        Assert.Equal("---\nname: docker\n---\nAdapted\n", flow.Draft);
        Assert.False(flow.Editing);
    }

    [Theory]
    [InlineData(typeof(AdaptationFailure.TimedOut))]
    [InlineData(typeof(AdaptationFailure.UsageLimitReached))]
    public void A_failure_the_user_has_to_read_moves_to_the_failed_step(Type failure)
    {
        var flow = ReadyFlow();
        flow.StartAdapting(Noon);

        flow.AdaptationFinished(Fail<AdaptedSkill>((AdaptationFailure)Activator.CreateInstance(failure)!));

        Assert.Equal(AdaptStep.Failed, flow.Step);
        Assert.IsType(failure, flow.Failure);
    }

    [Fact]
    public void A_failed_message_moves_to_the_failed_step_with_the_message()
    {
        var flow = ReadyFlow();
        flow.StartAdapting(Noon);

        flow.AdaptationFinished(Fail<AdaptedSkill>(new AdaptationFailure.Failed("boom")));

        Assert.Equal(new AdaptationFailure.Failed("boom"), flow.Failure);
    }

    [Fact]
    public void Cancelling_goes_back_to_the_form_without_an_error()
    {
        var flow = ReadyFlow();
        flow.StartAdapting(Noon);

        flow.AdaptationFinished(Fail<AdaptedSkill>(new AdaptationFailure.Cancelled()));

        Assert.Equal(AdaptStep.Form, flow.Step);
        Assert.Null(flow.Failure);
        Assert.NotNull(flow.Request);
    }

    [Theory]
    [InlineData(typeof(AdaptationFailure.NotInstalled), AiProviderState.NotInstalled)]
    [InlineData(typeof(AdaptationFailure.SignedOut), AiProviderState.SignedOut)]
    public void A_provider_that_is_not_usable_after_all_goes_back_to_the_form_in_that_state(Type failure, AiProviderState state)
    {
        var flow = ReadyFlow();
        flow.StartAdapting(Noon);

        flow.AdaptationFinished(Fail<AdaptedSkill>((AdaptationFailure)Activator.CreateInstance(failure)!));

        Assert.Equal(AdaptStep.Form, flow.Step);
        Assert.Equal(state, flow.Provider);
        Assert.False(flow.CanAdapt);
    }

    [Fact]
    public void Try_again_goes_back_to_the_form_with_everything_the_user_entered()
    {
        var flow = ReadyFlow();
        flow.EditInstructions("Keep it short.");
        flow.StartAdapting(Noon);
        flow.AdaptationFinished(Result<AdaptedSkill, AdaptationFailure>.Success(Adapted()));

        flow.TryAgain();

        Assert.Equal(AdaptStep.Form, flow.Step);
        Assert.Null(flow.Adapted);
        Assert.Equal("Keep it short.", flow.ExtraInstructions);
        Assert.Equal(Web, flow.TargetPlace);
        Assert.True(flow.CanAdapt);
    }

    [Fact]
    public void Editing_before_saving_turns_the_adapted_side_into_a_draft_the_user_changes()
    {
        var flow = ReadyFlow();
        flow.StartAdapting(Noon);
        flow.AdaptationFinished(Result<AdaptedSkill, AdaptationFailure>.Success(Adapted()));

        flow.EditBeforeSaving();
        flow.EditDraft("my own text\n");

        Assert.True(flow.Editing);
        Assert.Equal("my own text\n", flow.Draft);
    }

    [Fact]
    public void The_save_button_names_the_target_and_turns_into_an_overwrite_when_the_target_has_the_skill()
    {
        var flow = ReadyFlow();
        flow.StartAdapting(Noon);
        flow.AdaptationFinished(Result<AdaptedSkill, AdaptationFailure>.Success(Adapted(exists: false)));
        Assert.Equal("Save to Web", flow.SaveLabel);
        Assert.Equal(ConflictResolution.Skip, flow.Resolution);

        var existing = ReadyFlow();
        existing.StartAdapting(Noon);
        existing.AdaptationFinished(Result<AdaptedSkill, AdaptationFailure>.Success(Adapted(exists: true)));
        Assert.Equal("Overwrite in Web", existing.SaveLabel);
        Assert.Equal(ConflictResolution.Overwrite, existing.Resolution);
    }

    [Fact]
    public void A_skill_that_appeared_before_the_save_turns_the_save_into_an_overwrite_and_waits_for_the_users_choice()
    {
        var flow = ReadyFlow();
        flow.StartAdapting(Noon);
        flow.AdaptationFinished(Result<AdaptedSkill, AdaptationFailure>.Success(Adapted(exists: false)));

        flow.SaveRefused(SaveAdaptedSkillError.AlreadyExists);

        Assert.True(flow.Adapted!.ExistsInTarget);
        Assert.Equal("Overwrite in Web", flow.SaveLabel);
        Assert.Null(flow.SaveProblem);
    }

    [Fact]
    public void Other_save_errors_are_kept_for_the_review_to_show()
    {
        var flow = ReadyFlow();
        flow.StartAdapting(Noon);
        flow.AdaptationFinished(Result<AdaptedSkill, AdaptationFailure>.Success(Adapted()));

        flow.SaveRefused(SaveAdaptedSkillError.Failed);

        Assert.Equal(SaveAdaptedSkillError.Failed, flow.SaveProblem);
        Assert.Equal(AdaptStep.Review, flow.Step);
    }

    [Fact]
    public void Starting_remembers_when_it_began_and_has_no_text_yet()
    {
        var flow = ReadyFlow();

        flow.StartAdapting(Noon);

        Assert.Equal(Noon, flow.StartedAt);
        Assert.Equal("", flow.StreamedText);
    }

    [Fact]
    public void Text_that_arrives_while_running_is_added_in_order()
    {
        var flow = ReadyFlow();
        flow.StartAdapting(Noon);

        flow.Progressed(new AdaptationProgress.TextArrived("---\nna"));
        flow.Progressed(new AdaptationProgress.TextArrived("me: docker"));

        Assert.Equal("---\nname: docker", flow.StreamedText);
    }

    [Fact]
    public void Starting_over_throws_the_streamed_text_away()
    {
        var flow = ReadyFlow();
        flow.StartAdapting(Noon);
        flow.Progressed(new AdaptationProgress.TextArrived("first try"));

        flow.Progressed(new AdaptationProgress.StartedOver());
        flow.Progressed(new AdaptationProgress.TextArrived("second"));

        Assert.Equal("second", flow.StreamedText);
    }

    [Fact]
    public void Text_that_arrives_when_not_running_is_ignored()
    {
        var flow = ReadyFlow();

        flow.Progressed(new AdaptationProgress.TextArrived("late"));
        flow.StartAdapting(Noon);
        flow.AdaptationFinished(Fail<AdaptedSkill>(new AdaptationFailure.Cancelled()));
        flow.Progressed(new AdaptationProgress.TextArrived("later"));

        Assert.Equal("", flow.StreamedText);
    }

    [Fact]
    public void A_new_adaptation_starts_without_the_text_of_the_one_before()
    {
        var flow = ReadyFlow();
        flow.StartAdapting(Noon);
        flow.Progressed(new AdaptationProgress.TextArrived("old"));
        flow.AdaptationFinished(Fail<AdaptedSkill>(new AdaptationFailure.Cancelled()));

        flow.StartAdapting(Noon.AddMinutes(1));

        Assert.Equal("", flow.StreamedText);
        Assert.Equal(Noon.AddMinutes(1), flow.StartedAt);
    }

    [Fact]
    public void The_streamed_text_is_never_the_adapted_skill()
    {
        var flow = ReadyFlow();
        flow.StartAdapting(Noon);
        flow.Progressed(new AdaptationProgress.TextArrived("streamed only"));

        flow.AdaptationFinished(Result<AdaptedSkill, AdaptationFailure>.Success(Adapted()));

        Assert.Equal("---\nname: docker\n---\nAdapted\n", flow.Draft);
    }

    // ---- the provider is chosen apart from the target

    [Fact]
    public void Starts_with_the_provider_it_was_given_and_the_state_that_provider_is_in()
    {
        var flow = new AdaptFlow(Agent.ClaudeCode);

        flow.ProvidersChecked(
            new Dictionary<AiProviderId, AiProviderState> { [Claude] = AiProviderState.Ready, [Codex] = AiProviderState.SignedOut }, Codex);

        Assert.Equal(Codex, flow.ChosenProvider);
        Assert.Equal(AiProviderState.SignedOut, flow.Provider);
        Assert.Equal(AiProviderState.Ready, flow.ProviderStates[Claude]);
    }

    [Fact]
    public void Has_no_provider_state_before_the_check_has_finished()
    {
        var flow = new AdaptFlow(Agent.ClaudeCode);

        Assert.Null(flow.ChosenProvider);
        Assert.Null(flow.Provider);
        Assert.False(flow.CanAdapt);
    }

    [Fact]
    public void Choosing_a_provider_takes_the_state_of_that_provider()
    {
        var flow = ReadyFlow();
        Check(flow, AiProviderState.Ready, AiProviderState.NotInstalled);

        flow.ChooseProvider(Codex);

        Assert.Equal(Codex, flow.ChosenProvider);
        Assert.Equal(AiProviderState.NotInstalled, flow.Provider);
        Assert.False(flow.CanAdapt);
    }

    [Fact]
    public void Choosing_a_provider_never_changes_the_target_agent_the_target_place_or_the_request()
    {
        var flow = ReadyFlow();
        flow.ChooseAgent(Agent.ClaudeCode);
        flow.Prepared(Result<AdaptationRequest, PrepareAdaptationError>.Success(Request()));
        flow.EditInstructions("Keep it short.");
        var request = flow.Request;

        flow.ChooseProvider(Codex);

        Assert.Equal(Agent.ClaudeCode, flow.TargetAgent);
        Assert.Equal(Web, flow.TargetPlace);
        Assert.Same(request, flow.Request);
        Assert.Equal("Keep it short.", flow.ExtraInstructions);
        Assert.True(flow.CanAdapt);
    }

    [Fact]
    public void Choosing_a_provider_forgets_how_the_other_one_is_paid_for()
    {
        var flow = ReadyFlow();
        flow.UsageBasisChecked(Claude, AiUsageBasis.Subscription);

        flow.ChooseProvider(Codex);

        Assert.Equal(AiUsageBasis.Unknown, flow.UsageBasis);
    }

    [Fact]
    public void A_usage_basis_that_arrives_for_the_provider_that_is_no_longer_chosen_is_dropped()
    {
        var flow = ReadyFlow();
        flow.ChooseProvider(Codex);

        flow.UsageBasisChecked(Claude, AiUsageBasis.ApiKey);

        Assert.Equal(AiUsageBasis.Unknown, flow.UsageBasis);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_provider_cannot_be_chosen_once_an_adaptation_has_started(bool alreadyInReview)
    {
        var flow = ReadyFlow();
        flow.StartAdapting(Noon);
        if (alreadyInReview)
        {
            flow.AdaptationFinished(Result<AdaptedSkill, AdaptationFailure>.Success(Adapted()));
        }

        flow.ChooseProvider(Codex);

        Assert.Equal(Claude, flow.ChosenProvider);
    }

    [Fact]
    public void A_provider_cannot_be_chosen_while_waiting_for_a_sign_in()
    {
        var flow = ReadyFlow();
        flow.SignInStarted();

        flow.ChooseProvider(Codex);

        Assert.Equal(Claude, flow.ChosenProvider);
    }

    [Fact]
    public void The_sign_in_that_ends_changes_the_state_of_the_chosen_provider_only()
    {
        var flow = new AdaptFlow(Agent.ClaudeCode);
        Check(flow, AiProviderState.Ready, AiProviderState.SignedOut);
        flow.ChooseProvider(Codex);
        flow.SignInStarted();

        flow.SignInEnded(AiProviderState.Ready);

        Assert.Equal(AiProviderState.Ready, flow.ProviderStates[Codex]);
        Assert.Equal(AiProviderState.Ready, flow.ProviderStates[Claude]);
        Assert.Equal(AiProviderState.Ready, flow.Provider);
    }

    [Fact]
    public void A_provider_that_is_not_usable_after_all_is_marked_so_for_the_chosen_provider_only()
    {
        var flow = ReadyFlow();
        flow.ChooseProvider(Codex);
        flow.StartAdapting(Noon);

        flow.AdaptationFinished(Fail<AdaptedSkill>(new AdaptationFailure.SignedOut()));

        Assert.Equal(AiProviderState.SignedOut, flow.ProviderStates[Codex]);
        Assert.Equal(AiProviderState.Ready, flow.ProviderStates[Claude]);
    }
}
