using Scion.Application.Editing;
using Scion.Application.Skills;

namespace Scion.Application.Tests.Editing;

public class CheckSkillFrontmatterTests
{
    private readonly CheckSkillFrontmatter _check = new(new FakeFrontmatterParser());

    [Fact]
    public void A_valid_header_has_no_warnings()
    {
        var summary = _check.Execute("name: pdf\ndescription: Reads PDFs.", "pdf");

        Assert.Equal("pdf", summary.Name);
        Assert.Equal("Reads PDFs.", summary.Description);
        Assert.Empty(summary.Warnings);
    }

    [Fact]
    public void An_invalid_name_gives_a_warning_like_the_skill_list_does() =>
        Assert.Equal(
            [SkillWarning.NameHasUppercaseLetter],
            _check.Execute("name: PDF\ndescription: x", "pdf").Warnings);

    [Fact]
    public void A_name_that_differs_from_the_folder_gives_a_warning() =>
        Assert.Equal(
            [SkillWarning.NameDoesNotMatchFolder],
            _check.Execute("name: other\ndescription: x", "pdf").Warnings);

    [Fact]
    public void A_missing_description_gives_a_warning() =>
        Assert.Equal([SkillWarning.DescriptionMissing], _check.Execute("name: pdf", "pdf").Warnings);

    [Fact]
    public void An_unreadable_header_shows_the_folder_name_and_only_that_warning()
    {
        var summary = _check.Execute("UNREADABLE", "pdf");

        Assert.Equal("pdf", summary.Name);
        Assert.Equal("", summary.Description);
        Assert.Equal([SkillWarning.FrontmatterUnreadable], summary.Warnings);
    }

    [Fact]
    public void Carries_the_folder_name() =>
        Assert.Equal("pdf", _check.Execute("name: pdf\ndescription: x", "pdf").FolderName);
}
