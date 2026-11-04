namespace Scion.UI.Tests;

public class AvatarStyleTests
{
    [Theory]
    [InlineData("order-payment-api", "OP")]
    [InlineData("analytics_dashboard", "AD")]
    [InlineData("my.cool.project", "MC")]
    [InlineData("data-pipeline-orchestrator", "DP")]
    [InlineData("Billing Worker", "BW")]
    [InlineData("-api--gateway", "AG")]
    public void Initials_are_the_first_letters_of_the_first_two_words(string name, string expected)
    {
        Assert.Equal(expected, AvatarStyle.Initials(name));
    }

    [Theory]
    [InlineData("scion", "SC")]
    [InlineData("x", "X")]
    public void Initials_of_a_single_word_are_its_first_two_letters(string name, string expected)
    {
        Assert.Equal(expected, AvatarStyle.Initials(name));
    }

    [Theory]
    [InlineData("x", AvatarTone.Sage)]
    [InlineData("web", AvatarTone.Slate)]
    [InlineData("api", AvatarTone.Rose)]
    [InlineData("scion", AvatarTone.Lavender)]
    [InlineData("telemetry-exporter", AvatarTone.Ochre)]
    [InlineData("auth-gateway", AvatarTone.Teal)]
    [InlineData("billing-worker", AvatarTone.Clay)]
    [InlineData("a", AvatarTone.Steel)]
    [InlineData("alpha", AvatarTone.Moss)]
    public void Tone_is_the_palette_entry_at_the_fnv_1a_hash_of_the_name_modulo_nine(string name, AvatarTone expected)
    {
        Assert.Equal(expected, AvatarStyle.Tone(name));
    }

    [Fact]
    public void Tone_is_a_defined_palette_entry_for_any_name()
    {
        foreach (var name in new[] { "", "a", "ünïcode-ñame", new string('z', 500) })
        {
            Assert.True(Enum.IsDefined(AvatarStyle.Tone(name)));
        }
    }

    [Fact]
    public void The_palette_has_nine_tones_in_the_order_of_the_design()
    {
        Assert.Equal(
            ["Sage", "Slate", "Rose", "Ochre", "Lavender", "Teal", "Clay", "Steel", "Moss"],
            Enum.GetNames<AvatarTone>());
    }
}
