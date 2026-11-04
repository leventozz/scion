using Scion.Domain;

namespace Scion.Domain.Tests.Projects;

public class RegisteredProjectTests
{
    [Fact]
    public void Open_returns_a_copy_with_the_new_last_opened_time_and_everything_else_unchanged()
    {
        var project = new RegisteredProject(
            "/work/api", "api", new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));
        var now = new DateTimeOffset(2026, 3, 5, 12, 30, 0, TimeSpan.FromHours(3));

        var opened = project.Open(now);

        Assert.Equal(new RegisteredProject("/work/api", "api", now), opened);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), project.LastOpenedAt);
    }

    private static readonly DateTimeOffset OpenedAt = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Rename_returns_a_copy_with_the_new_name_and_everything_else_unchanged()
    {
        var project = new RegisteredProject("/work/api", "api", OpenedAt);

        var result = project.Rename("Our API");

        Assert.True(result.IsSuccess);
        Assert.Equal(new RegisteredProject("/work/api", "Our API", OpenedAt), result.Value);
    }

    [Fact]
    public void Rename_trims_the_ends_and_keeps_inner_spaces()
    {
        var project = new RegisteredProject("/work/api", "api", OpenedAt);

        var result = project.Rename("   Our   API \t");

        Assert.Equal("Our   API", result.Value.DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Rename_rejects_a_name_that_is_empty_after_trimming(string name)
    {
        var project = new RegisteredProject("/work/api", "api", OpenedAt);

        var result = project.Rename(name);

        Assert.False(result.IsSuccess);
        Assert.IsType<DisplayNameError.Empty>(result.Error);
    }
}
