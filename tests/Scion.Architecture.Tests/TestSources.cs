namespace Scion.Architecture.Tests;

/// <summary>The sources of every test project, for the rules that check what a test may not do.</summary>
internal static class TestSources
{
    /// <summary>Every test source except the files named: a rule's own file spells out what it forbids.</summary>
    public static IEnumerable<string> Except(params string[] ruleFileNames) =>
        Directory.EnumerateFiles(Root(), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !ruleFileNames.Contains(Path.GetFileName(file)));

    public static string Read(string project, string fileName) => File.ReadAllText(Path.Combine(Root(), project, fileName));

    /// <summary>A file of the repository itself (a script, a workflow, a plist), by its path from the repository root.</summary>
    public static string ReadRepositoryFile(string relativePath) => File.ReadAllText(RepositoryFilePath(relativePath));

    public static string RepositoryFilePath(string relativePath) => Path.Combine(RepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string Root() => Path.Combine(RepositoryRoot(), "tests");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "scion.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("scion.slnx not found.");
    }
}
