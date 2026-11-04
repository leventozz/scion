namespace Scion.Domain.Skills;

public sealed record SkillDescription
{
    public const int MaxLength = 1024;

    private SkillDescription(string value) => Value = value;

    public string Value { get; }

    public static Result<SkillDescription, SkillDescriptionError> Create(string value)
    {
        var error = Validate(value);

        return error is null
            ? Result<SkillDescription, SkillDescriptionError>.Success(new SkillDescription(value))
            : Result<SkillDescription, SkillDescriptionError>.Failure(error.Value);
    }

    private static SkillDescriptionError? Validate(string value)
    {
        if (value.Length == 0) return SkillDescriptionError.Empty;
        if (value.Length > MaxLength) return SkillDescriptionError.TooLong;

        return null;
    }
}
