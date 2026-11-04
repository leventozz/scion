using Scion.Application.Viewing;

namespace Scion.Application.Tests.Viewing;

public class RelativeFilePathTests
{
    [Theory]
    [InlineData("SKILL.md")]
    [InlineData("scripts/run.sh")]
    [InlineData("references/deep/nested/file.md")]
    [InlineData("name.with.dots.txt")]
    public void Accepts_relative_paths_inside_the_folder(string path)
    {
        Assert.True(RelativeFilePath.IsSafe(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("C:file.txt")]
    [InlineData("..")]
    [InlineData("../secret.txt")]
    [InlineData("scripts/../../secret.txt")]
    [InlineData("scripts/./run.sh")]
    [InlineData("scripts\\run.sh")]
    [InlineData(".hidden")]
    [InlineData("scripts/.hidden/file.txt")]
    [InlineData(".git/config")]
    [InlineData("scripts//run.sh")]
    [InlineData("scripts/")]
    public void Rejects_everything_that_could_leave_the_folder_or_reach_a_hidden_entry(string path)
    {
        Assert.False(RelativeFilePath.IsSafe(path));
    }
}
