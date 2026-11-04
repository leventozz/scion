using System.Reflection;
using NetArchTest.Rules;
using Scion.Application.Projects;
using Scion.Domain;
using Scion.Infrastructure.ClaudeCode;
using Scion.Infrastructure.Codex;
using Scion.Infrastructure.FileSystem;
using Scion.Infrastructure.LocalClients;
using Scion.Infrastructure.ProjectRegistry;

namespace Scion.Architecture.Tests;

/// <summary>The layers of the solution as assemblies and namespaces, plus the two ways the rules look at them.</summary>
internal static class Layers
{
    public const string DomainNamespace = "Scion.Domain";
    public const string ApplicationNamespace = "Scion.Application";
    public const string InfrastructureNamespace = "Scion.Infrastructure";
    public const string UiNamespace = "Scion.UI";
    public const string HostNamespace = "Scion.Host";
    public const string LocalClientsNamespace = "Scion.Infrastructure.LocalClients";

    /// <summary>The adapters: infrastructure that plugs one detail (a disk layout, a registry file, an AI client) into a port.</summary>
    public static readonly string[] AdapterNamespaces =
    [
        "Scion.Infrastructure.FileSystem",
        "Scion.Infrastructure.ProjectRegistry",
        "Scion.Infrastructure.ClaudeCode",
        "Scion.Infrastructure.Codex",
    ];

    /// <summary>Libraries that only an outer layer may use.</summary>
    public static readonly string[] Packages =
    [
        "YamlDotNet",
        "Markdig",
        "Microsoft.AspNetCore",
        "Microsoft.Extensions",
        "Photino",
        "Anthropic",
    ];

    public static Assembly Domain { get; } = typeof(Agent).Assembly;

    public static Assembly Application { get; } = typeof(ListProjects).Assembly;

    public static Assembly ClaudeCode { get; } = typeof(ClaudeCodeProvider).Assembly;

    public static Assembly Codex { get; } = typeof(CodexProvider).Assembly;

    public static Assembly FileSystem { get; } = typeof(FileSystemSkillFolderReader).Assembly;

    /// <summary>The library the adapters of local clients share. It is infrastructure, but no adapter itself.</summary>
    public static Assembly LocalClients { get; } = typeof(LocalClientExecutable).Assembly;

    public static Assembly ProjectRegistry { get; } = typeof(JsonProjectRegistry).Assembly;

    public static Assembly Ui { get; } = typeof(Scion.UI.CopyPlace).Assembly;

    public static IEnumerable<object[]> NonHostAssemblies() =>
        [[Domain], [Application], [FileSystem], [ProjectRegistry], [ClaudeCode], [Codex], [LocalClients], [Ui]];

    public static IEnumerable<object[]> InfrastructureAssemblies() =>
        [[FileSystem], [ProjectRegistry], [ClaudeCode], [Codex], [LocalClients]];

    public static IEnumerable<object[]> AdapterAssemblies() => [[FileSystem], [ProjectRegistry], [ClaudeCode], [Codex]];

    /// <summary>Names of the assemblies this assembly was compiled against.</summary>
    public static IReadOnlyList<string> ReferencesOf(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name!).ToList();

    public static bool IsFramework(string assemblyName) =>
        assemblyName is "mscorlib" or "netstandard"
        || assemblyName.StartsWith("System", StringComparison.Ordinal)
        || assemblyName.StartsWith("Microsoft.Win32.", StringComparison.Ordinal);

    /// <summary>Type-level check: no type of the assembly uses anything from these namespaces.</summary>
    public static void AssertNoTypeDependsOn(Assembly assembly, params string[] namespaces)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(namespaces).GetResult();

        Assert.True(
            result.IsSuccessful,
            $"{assembly.GetName().Name} must not depend on [{string.Join(", ", namespaces)}], but these types do: "
            + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
