namespace Scion.Domain.Skills;

public sealed record SkillName
{
    public const int MaxLength = 64;

    private const char Hyphen = '-';

    private SkillName(string value) => Value = value;

    public string Value { get; }

    public static Result<SkillName, SkillNameError> Create(string value)
    {
        var error = Validate(value);

        return error is null
            ? Result<SkillName, SkillNameError>.Success(new SkillName(value))
            : Result<SkillName, SkillNameError>.Failure(error.Value);
    }

    private static SkillNameError? Validate(string value)
    {
        if (value.Length == 0) return SkillNameError.Empty;
        if (value.Length > MaxLength) return SkillNameError.TooLong;
        if (value.Any(IsUppercaseAsciiLetter)) return SkillNameError.UppercaseLetter;
        if (!value.All(IsAllowedCharacter)) return SkillNameError.DisallowedCharacter;
        if (value.StartsWith(Hyphen) || value.EndsWith(Hyphen)) return SkillNameError.LeadingOrTrailingHyphen;
        if (value.Contains("--")) return SkillNameError.ConsecutiveHyphens;

        return null;
    }

    private static bool IsUppercaseAsciiLetter(char c) => c is >= 'A' and <= 'Z';

    private static bool IsAllowedCharacter(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or Hyphen;
}
