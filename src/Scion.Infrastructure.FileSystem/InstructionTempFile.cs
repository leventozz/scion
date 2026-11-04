using System.Text.RegularExpressions;

namespace Scion.Infrastructure.FileSystem;

/// <summary>
/// The temp file a save of an instruction file stages its content in, beside the target:
/// <c>.{name}.scion-{guid}.tmp</c>. The one place that knows the name, so what a save creates is exactly what the
/// start-up cleanup recognises.
/// </summary>
internal static partial class InstructionTempFile
{
    public static string NewPathBeside(string target) =>
        Path.Combine(Path.GetDirectoryName(target)!, $".{Path.GetFileName(target)}.scion-{Guid.NewGuid():N}.tmp");

    public static bool IsLeftoverOf(string targetName, string fileName) =>
        LeftoverPattern().Match(fileName) is { Success: true } match && match.Groups["name"].Value == targetName;

    [GeneratedRegex(@"^\.(?<name>.+)\.scion-[0-9a-f]{32}\.tmp$")]
    private static partial Regex LeftoverPattern();
}
