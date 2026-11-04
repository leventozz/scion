using System.Text.Json;

namespace Scion.Smoke.Tests;

internal static class Fixtures
{
    /// <summary>A project folder with one Claude skill named "x".</summary>
    public static string CreateProjectFolder(TempDirectory parent, string name)
    {
        var projectFolder = Path.Combine(parent.Path, name);
        var skillFolder = Path.Combine(projectFolder, ".claude", "skills", "x");
        Directory.CreateDirectory(skillFolder);
        File.WriteAllText(
            Path.Combine(skillFolder, "SKILL.md"), "---\nname: x\ndescription: A test skill.\n---\nBody\n");

        return projectFolder;
    }

    public static void WriteRegistry(TempDirectory scionHome, params (string Path, string DisplayName)[] projects)
    {
        var document = new
        {
            projects = projects.Select(project => new
            {
                path = project.Path,
                displayName = project.DisplayName,
                lastOpenedAt = DateTimeOffset.UtcNow,
            }),
        };

        WriteRegistryText(scionHome, JsonSerializer.Serialize(document));
    }

    public static void WriteRegistryText(TempDirectory scionHome, string content)
    {
        var registryFile = Path.Combine(scionHome.Path, ".scion", "projects.json");
        Directory.CreateDirectory(Path.GetDirectoryName(registryFile)!);
        File.WriteAllText(registryFile, content);
    }

    public static JsonElement[] ReadRegisteredProjects(DesktopApp app) =>
        JsonDocument.Parse(File.ReadAllText(app.RegistryFile)).RootElement.GetProperty("projects").EnumerateArray().ToArray();
}
