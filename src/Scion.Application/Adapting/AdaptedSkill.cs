using Scion.Application.Skills;

namespace Scion.Application.Adapting;

/// <param name="Text">The adapted SKILL.md with LF line breaks and the source skill's name.</param>
/// <param name="Check">The header of <paramref name="Text"/> as the skill list would show it, warnings included.</param>
/// <param name="ExistsInTarget">The target already has a skill folder with this name.</param>
public sealed record AdaptedSkill(string Text, SkillSummary Check, bool ExistsInTarget);
