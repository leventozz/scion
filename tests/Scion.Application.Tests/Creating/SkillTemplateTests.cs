using Scion.Application.Creating;

namespace Scion.Application.Tests.Creating;

public class SkillTemplateTests
{
    [Fact]
    public void A_new_skill_is_its_frontmatter_followed_by_one_blank_line_and_no_body() =>
        Assert.Equal(
            "---\nname: a\n---\n\n",
            SkillTemplate.Compose("---\nname: a\n---\n"));
}
