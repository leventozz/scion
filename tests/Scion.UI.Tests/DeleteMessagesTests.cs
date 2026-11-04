namespace Scion.UI.Tests;

public class DeleteMessagesTests
{
    [Fact]
    public void The_body_says_where_the_skill_goes_and_that_it_can_come_back() =>
        Assert.Equal(
            "The skill folder and all its files will be moved to Scion's trash. You can restore it later.",
            DeleteMessages.Body);

    [Fact]
    public void A_failed_move_has_its_own_sentence() =>
        Assert.Equal("The skill couldn't be deleted.", DeleteMessages.Failed);

    [Fact]
    public void A_half_removed_original_says_the_skill_is_safe_in_the_trash() =>
        Assert.Equal(
            "Moved to Scion's trash, but some files couldn't be removed from the skill folder.",
            DeleteMessages.OriginalNotFullyRemoved);
}
