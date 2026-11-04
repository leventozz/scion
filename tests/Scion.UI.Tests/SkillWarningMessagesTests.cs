using Scion.Application.Skills;

namespace Scion.UI.Tests;

public class SkillWarningMessagesTests
{
    public static TheoryData<SkillWarning> AllWarnings => new(Enum.GetValues<SkillWarning>());

    [Theory]
    [MemberData(nameof(AllWarnings))]
    public void Every_warning_has_a_message(SkillWarning warning)
    {
        var message = SkillWarningMessages.For(warning, ASkill());

        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Fact]
    public void The_folder_mismatch_message_quotes_the_folder_name()
    {
        var message = SkillWarningMessages.For(SkillWarning.NameDoesNotMatchFolder, ASkill(folderName: "compose"));

        Assert.Equal("Name doesn't match the folder name \"compose\".", message);
    }

    private static SkillSummary ASkill(string folderName = "docker-compose") =>
        new("docker-compose", folderName, "Runs compose files.", [], CanBeDeleted: true);
}
