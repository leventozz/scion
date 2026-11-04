using Microsoft.Extensions.Time.Testing;
using Scion.Application.Adapting;
using Scion.Application.Editing;
using Scion.Application.Skills;
using Scion.Application.Skills.Copying;
using Scion.Application.Tests.Editing;
using Scion.Application.Tests.Skills.Copying;

namespace Scion.Application.Tests.Adapting;

public class AdaptSkillTests
{
    private static readonly AdaptationRequest Request = AdaptationRequests.Sample();

    private static readonly AiProviderId Provider = new("sample");

    private readonly FakeSkillAdapter _adapter = new();
    private readonly InMemorySkillFolders _folders = new();
    private readonly FakeTimeProvider _clock = new();
    private readonly RecordingProgress _progress = new();

    private AdaptSkill AdaptSkillWith(StringComparison comparison = StringComparison.Ordinal) =>
        new(
            new AiProviderPortsStub<ISkillAdapter>().With("sample", _adapter),
            new FakeSkillNameSetter(),
            new CheckSkillFrontmatter(new FakeFrontmatterParser()),
            _folders,
            new PathComparison(comparison),
            _clock);

    [Fact]
    public async Task Asks_only_the_adapter_of_the_provider_it_was_given()
    {
        var other = new FakeSkillAdapter();
        var adaptSkill = new AdaptSkill(
            new AiProviderPortsStub<ISkillAdapter>().With("sample", _adapter).With("other", other),
            new FakeSkillNameSetter(),
            new CheckSkillFrontmatter(new FakeFrontmatterParser()),
            _folders,
            new PathComparison(StringComparison.Ordinal),
            _clock);

        await adaptSkill.ExecuteAsync(new AiProviderId("other"), Request, _progress);

        Assert.Single(other.Prompts);
        Assert.Empty(_adapter.Prompts);
    }

    [Fact]
    public async Task Hands_the_prompt_built_from_the_request_to_the_adapter()
    {
        await AdaptSkillWith().ExecuteAsync(Provider, Request, _progress);

        Assert.Equal(AdaptationPrompt.For(Request), Assert.Single(_adapter.Prompts));
    }

    [Fact]
    public async Task Hands_over_the_extra_instructions_the_request_carries()
    {
        var request = Request.WithExtraInstructions("We use PostgreSQL.");

        await AdaptSkillWith().ExecuteAsync(Provider, request, _progress);

        Assert.Contains("We use PostgreSQL.", Assert.Single(_adapter.Prompts).Content);
    }

    [Fact]
    public async Task Sets_the_name_to_the_name_of_the_source_skill()
    {
        _adapter.Returns("name: renamed-by-claude\ndescription: Adapted\n");

        var result = await AdaptSkillWith().ExecuteAsync(Provider, Request, _progress);

        Assert.Equal("name: docker\ndescription: Adapted\n", result.Value.Text);
    }

    [Fact]
    public async Task Keeps_the_text_as_it_is_when_its_name_cannot_be_set()
    {
        _adapter.Returns("UNREADABLE\nbody");

        var result = await AdaptSkillWith().ExecuteAsync(Provider, Request, _progress);

        Assert.Equal("UNREADABLE\nbody", result.Value.Text);
        Assert.Contains(SkillWarning.FrontmatterUnreadable, result.Value.Check.Warnings);
    }

    [Fact]
    public async Task Gives_the_text_LF_line_breaks()
    {
        _adapter.Returns("name: x\r\ndescription: Adapted\r\n");

        var result = await AdaptSkillWith().ExecuteAsync(Provider, Request, _progress);

        Assert.Equal("name: docker\ndescription: Adapted\n", result.Value.Text);
    }

    [Fact]
    public async Task Reports_the_usual_warnings_of_the_adapted_header()
    {
        _adapter.Returns("name: x\n# no description\n");

        var result = await AdaptSkillWith().ExecuteAsync(Provider, Request, _progress);

        Assert.Equal([SkillWarning.DescriptionMissing], result.Value.Check.Warnings);
    }

    [Fact]
    public async Task A_clean_header_has_no_warnings()
    {
        var result = await AdaptSkillWith().ExecuteAsync(Provider, Request, _progress);

        Assert.Empty(result.Value.Check.Warnings);
    }

    [Fact]
    public async Task Says_when_the_target_already_has_a_skill_with_the_folder_name()
    {
        _folders.Add(Request.Target, "docker");

        var result = await AdaptSkillWith().ExecuteAsync(Provider, Request, _progress);

        Assert.True(result.Value.ExistsInTarget);
    }

    [Fact]
    public async Task Says_nothing_exists_when_the_target_has_other_skills_only()
    {
        _folders.Add(Request.Target, "testing");

        var result = await AdaptSkillWith().ExecuteAsync(Provider, Request, _progress);

        Assert.False(result.Value.ExistsInTarget);
    }

    [Fact]
    public async Task Compares_the_folder_names_the_way_the_file_system_does()
    {
        _folders.Add(Request.Target, "Docker");

        var caseSensitive = await AdaptSkillWith(StringComparison.Ordinal).ExecuteAsync(Provider, Request, _progress);
        var caseInsensitive = await AdaptSkillWith(StringComparison.OrdinalIgnoreCase).ExecuteAsync(Provider, Request, _progress);

        Assert.False(caseSensitive.Value.ExistsInTarget);
        Assert.True(caseInsensitive.Value.ExistsInTarget);
    }

    public static TheoryData<AdaptationFailure> Failures => new()
    {
        new AdaptationFailure.NotInstalled(),
        new AdaptationFailure.SignedOut(),
        new AdaptationFailure.UsageLimitReached(),
        new AdaptationFailure.Failed("it broke"),
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task Passes_every_failure_of_the_adapter_on(AdaptationFailure failure)
    {
        _adapter.FailsWith(failure);

        var result = await AdaptSkillWith().ExecuteAsync(Provider, Request, _progress);

        Assert.Equal(failure, result.Error);
    }

    [Fact]
    public async Task A_blank_answer_is_a_failure_and_not_an_empty_skill()
    {
        _adapter.Returns("  \n ");

        var result = await AdaptSkillWith().ExecuteAsync(Provider, Request, _progress);

        Assert.IsType<AdaptationFailure.Failed>(result.Error);
    }

    [Fact]
    public async Task Is_cancelled_when_the_user_cancels()
    {
        _adapter.Hangs();
        using var cancellation = new CancellationTokenSource();

        var task = AdaptSkillWith().ExecuteAsync(Provider, Request, _progress, cancellation.Token);
        await cancellation.CancelAsync();
        var result = await task;

        Assert.IsType<AdaptationFailure.Cancelled>(result.Error);
    }

    [Fact]
    public async Task Is_cancelled_even_when_the_adapter_throws_on_cancellation()
    {
        _adapter.HangsAndThrowsWhenCancelled();
        using var cancellation = new CancellationTokenSource();

        var task = AdaptSkillWith().ExecuteAsync(Provider, Request, _progress, cancellation.Token);
        await cancellation.CancelAsync();
        var result = await task;

        Assert.IsType<AdaptationFailure.Cancelled>(result.Error);
    }

    [Fact]
    public async Task Times_out_after_five_minutes()
    {
        _adapter.Hangs();

        var task = AdaptSkillWith().ExecuteAsync(Provider, Request, _progress);
        _clock.Advance(TimeSpan.FromMinutes(5));
        var result = await task;

        Assert.IsType<AdaptationFailure.TimedOut>(result.Error);
    }

    [Fact]
    public async Task Times_out_when_the_adapter_throws_on_cancellation_too()
    {
        _adapter.HangsAndThrowsWhenCancelled();

        var task = AdaptSkillWith().ExecuteAsync(Provider, Request, _progress);
        _clock.Advance(TimeSpan.FromMinutes(5));
        var result = await task;

        Assert.IsType<AdaptationFailure.TimedOut>(result.Error);
    }

    [Fact]
    public async Task Does_not_time_out_before_the_five_minutes_are_over()
    {
        _adapter.Hangs();
        using var cancellation = new CancellationTokenSource();

        var task = AdaptSkillWith().ExecuteAsync(Provider, Request, _progress, cancellation.Token);
        _clock.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59));
        await cancellation.CancelAsync();
        var result = await task;

        Assert.IsType<AdaptationFailure.Cancelled>(result.Error);
    }

    [Fact]
    public async Task Passes_what_the_adapter_reports_on_to_the_caller_in_order()
    {
        _adapter.ReportsBeforeAnswering(
            new AdaptationProgress.TextArrived("---\nname"),
            new AdaptationProgress.StartedOver(),
            new AdaptationProgress.TextArrived("---\nname: docker"));

        await AdaptSkillWith().ExecuteAsync(Provider, Request, _progress);

        Assert.Equal(
            [
                new AdaptationProgress.TextArrived("---\nname"),
                new AdaptationProgress.StartedOver(),
                new AdaptationProgress.TextArrived("---\nname: docker"),
            ],
            _progress.Reports);
    }

    [Fact]
    public async Task The_result_is_the_text_the_adapter_returns_never_the_streamed_pieces()
    {
        _adapter.ReportsBeforeAnswering(new AdaptationProgress.TextArrived("streamed only"));
        _adapter.Returns("name: x\ndescription: Final\n");

        var result = await AdaptSkillWith().ExecuteAsync(Provider, Request, _progress);

        Assert.Equal("name: docker\ndescription: Final\n", result.Value.Text);
    }

    [Fact]
    public async Task Cancelling_after_text_arrived_gives_cancelled_and_no_skill()
    {
        _adapter.ReportsBeforeAnswering(new AdaptationProgress.TextArrived("---\nname: docker\n"));
        _adapter.Hangs();
        using var cancellation = new CancellationTokenSource();

        var task = AdaptSkillWith().ExecuteAsync(Provider, Request, _progress, cancellation.Token);
        await cancellation.CancelAsync();
        var result = await task;

        Assert.False(result.IsSuccess);
        Assert.IsType<AdaptationFailure.Cancelled>(result.Error);
        Assert.Single(_progress.Texts);
    }

    [Fact]
    public async Task A_failure_after_text_arrived_is_that_failure_and_no_skill()
    {
        _adapter.ReportsBeforeAnswering(new AdaptationProgress.TextArrived("---\nname: docker\n"));
        _adapter.FailsWith(new AdaptationFailure.Failed("The stream stopped."));

        var result = await AdaptSkillWith().ExecuteAsync(Provider, Request, _progress);

        Assert.Equal(new AdaptationFailure.Failed("The stream stopped."), result.Error);
    }
}
