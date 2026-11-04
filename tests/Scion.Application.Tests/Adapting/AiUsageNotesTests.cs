using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

public class AiUsageNotesTests
{
    private static readonly AiUsageNotes Notes = new("Counts toward your plan.", "Uses your API key.");

    [Theory]
    [InlineData(AiUsageBasis.Subscription, "Counts toward your plan.")]
    [InlineData(AiUsageBasis.ApiKey, "Uses your API key.")]
    [InlineData(AiUsageBasis.Unknown, null)]
    public void Has_the_line_of_the_basis_the_client_reported_and_none_for_an_unknown_one(AiUsageBasis basis, string? expected) =>
        Assert.Equal(expected, Notes.For(basis));

    [Fact]
    public void A_provider_without_a_line_for_a_basis_says_nothing_for_it() =>
        Assert.Null(new AiUsageNotes(null, "Uses your API key.").For(AiUsageBasis.Subscription));

    [Theory]
    [InlineData(AiUsageBasis.Subscription)]
    [InlineData(AiUsageBasis.ApiKey)]
    [InlineData(AiUsageBasis.Unknown)]
    public void A_line_that_holds_whatever_the_client_reports_is_the_same_for_every_basis(AiUsageBasis basis) =>
        Assert.Equal("Fixed line.", AiUsageNotes.Always("Fixed line.").For(basis));

    [Fact]
    public void A_line_for_an_unknown_basis_is_shown_when_the_client_says_nothing() =>
        Assert.Equal("Not told.", new AiUsageNotes(null, null, "Not told.").For(AiUsageBasis.Unknown));
}
