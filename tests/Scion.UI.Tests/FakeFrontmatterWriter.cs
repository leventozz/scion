using Scion.Application.Creating;
using Scion.Domain.Skills;

namespace Scion.UI.Tests;

internal sealed class FakeFrontmatterWriter : ISkillFrontmatterWriter
{
    public string Write(SkillName name, SkillDescription description) =>
        $"---\nname: {name.Value}\ndescription: {description.Value}\n---\n";
}
