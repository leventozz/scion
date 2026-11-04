using Scion.Application.Skills;
using Scion.Domain.Skills;

namespace Scion.Application.Tests.Skills;

public class SkillWarningsTests
{
    public static TheoryData<SkillNameError> AllNameErrors() => new(Enum.GetValues<SkillNameError>());

    public static TheoryData<SkillDescriptionError> AllDescriptionErrors() =>
        new(Enum.GetValues<SkillDescriptionError>());

    [Theory]
    [MemberData(nameof(AllNameErrors))]
    public void Every_name_error_maps_to_a_warning(SkillNameError error)
    {
        var exception = Record.Exception(() => SkillWarnings.From(error));

        Assert.Null(exception);
    }

    [Theory]
    [MemberData(nameof(AllDescriptionErrors))]
    public void Every_description_error_maps_to_a_warning(SkillDescriptionError error)
    {
        var exception = Record.Exception(() => SkillWarnings.From(error));

        Assert.Null(exception);
    }

    [Fact]
    public void Every_name_error_maps_to_a_distinct_warning()
    {
        var warnings = Enum.GetValues<SkillNameError>().Select(SkillWarnings.From).ToList();

        Assert.Equal(warnings.Count, warnings.Distinct().Count());
    }

    [Fact]
    public void Every_description_error_maps_to_a_distinct_warning()
    {
        var warnings = Enum.GetValues<SkillDescriptionError>().Select(SkillWarnings.From).ToList();

        Assert.Equal(warnings.Count, warnings.Distinct().Count());
    }
}
