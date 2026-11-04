using System.Reflection;
using NetArchTest.Rules;

namespace Scion.Architecture.Tests;

/// <summary>
/// The Dependency Rule: source-code dependencies point only inward.
/// Domain ← Application ← Infrastructure / UI ← Host.
/// </summary>
public class DependencyRuleTests
{
    private static readonly string[] OuterThanDomain =
        [Layers.ApplicationNamespace, Layers.InfrastructureNamespace, Layers.UiNamespace, Layers.HostNamespace];

    private static readonly string[] OuterThanApplication =
        [Layers.InfrastructureNamespace, Layers.UiNamespace, Layers.HostNamespace];

    [Fact]
    public void Domain_references_no_other_project_and_no_package()
    {
        var notFramework = Layers.ReferencesOf(Layers.Domain).Where(name => !Layers.IsFramework(name));

        Assert.Empty(notFramework);
    }

    [Fact]
    public void Domain_types_use_nothing_from_an_outer_layer_or_a_package()
    {
        Layers.AssertNoTypeDependsOn(Layers.Domain, [.. OuterThanDomain, .. Layers.Packages]);
    }

    [Fact]
    public void Application_references_only_the_domain_among_projects_and_no_package()
    {
        var allowed = new[] { "Scion.Domain" };

        var unexpected = Layers.ReferencesOf(Layers.Application)
            .Where(name => !Layers.IsFramework(name) && !allowed.Contains(name));

        Assert.Empty(unexpected);
    }

    [Fact]
    public void Application_types_use_nothing_from_an_outer_layer_or_a_package()
    {
        Layers.AssertNoTypeDependsOn(Layers.Application, [.. OuterThanApplication, .. Layers.Packages]);
    }

    [Theory]
    [MemberData(nameof(Layers.InfrastructureAssemblies), MemberType = typeof(Layers))]
    public void Infrastructure_knows_neither_the_ui_nor_the_host(Assembly infrastructure)
    {
        var outer = Layers.ReferencesOf(infrastructure)
            .Where(name => name.StartsWith(Layers.UiNamespace) || name.StartsWith(Layers.HostNamespace));

        Assert.Empty(outer);
        Layers.AssertNoTypeDependsOn(infrastructure, Layers.UiNamespace, Layers.HostNamespace);
    }

    [Fact]
    public void Local_clients_reference_only_the_framework_and_use_nothing_from_another_layer_or_a_package()
    {
        var notFramework = Layers.ReferencesOf(Layers.LocalClients).Where(name => !Layers.IsFramework(name));

        Assert.Empty(notFramework);
        Layers.AssertNoTypeDependsOn(
            Layers.LocalClients,
            [Layers.DomainNamespace, Layers.ApplicationNamespace, Layers.UiNamespace, Layers.HostNamespace,
                .. Layers.AdapterNamespaces, .. Layers.Packages]);
    }

    /// <summary>An adapter is a plugin: it never knows another one. The only thing adapters share is the local-clients library.</summary>
    [Theory]
    [MemberData(nameof(Layers.AdapterAssemblies), MemberType = typeof(Layers))]
    public void An_adapter_knows_no_other_adapter(Assembly adapter)
    {
        var own = adapter.GetName().Name!;
        var otherAdapters = Layers.AdapterNamespaces.Where(name => !own.StartsWith(name)).ToArray();

        var knownOthers = Layers.ReferencesOf(adapter).Where(name => otherAdapters.Any(name.StartsWith));

        Assert.Empty(knownOthers);
        Layers.AssertNoTypeDependsOn(adapter, otherAdapters);
    }

    [Fact]
    public void Ui_knows_no_infrastructure_and_not_the_host()
    {
        var outer = Layers.ReferencesOf(Layers.Ui)
            .Where(name => name.StartsWith(Layers.InfrastructureNamespace) || name.StartsWith(Layers.HostNamespace));

        Assert.Empty(outer);
        Layers.AssertNoTypeDependsOn(Layers.Ui, Layers.InfrastructureNamespace, Layers.HostNamespace);
    }

    [Theory]
    [MemberData(nameof(Layers.NonHostAssemblies), MemberType = typeof(Layers))]
    public void No_project_depends_on_the_host(Assembly assembly)
    {
        Assert.DoesNotContain(Layers.ReferencesOf(assembly), name => name.StartsWith(Layers.HostNamespace));
        Layers.AssertNoTypeDependsOn(assembly, Layers.HostNamespace);
    }

    [Fact]
    public void Only_the_host_knows_the_infrastructure_adapters()
    {
        var withoutHost = new[] { Layers.Domain, Layers.Application, Layers.Ui };

        var knowers = withoutHost
            .Where(assembly => Layers.ReferencesOf(assembly).Any(name => name.StartsWith(Layers.InfrastructureNamespace)))
            .Select(assembly => assembly.GetName().Name);

        Assert.Empty(knowers);
    }

    /// <summary>Guards against rules that pass because they look at nothing: the detector must see real dependencies.</summary>
    [Fact]
    public void The_rules_do_see_the_dependencies_that_do_exist()
    {
        Assert.NotEmpty(Types.InAssembly(Layers.Domain).GetTypes());
        Assert.NotEmpty(Types.InAssembly(Layers.Application).That().HaveDependencyOn(Layers.DomainNamespace).GetTypes());
        Assert.NotEmpty(Types.InAssembly(Layers.Ui).That().HaveDependencyOn(Layers.ApplicationNamespace).GetTypes());
        Assert.NotEmpty(Types.InAssembly(Layers.FileSystem).That().HaveDependencyOn(Layers.ApplicationNamespace).GetTypes());
        Assert.Contains("Scion.Domain", Layers.ReferencesOf(Layers.Application));
        Assert.Contains("YamlDotNet", Layers.ReferencesOf(Layers.FileSystem));
        Assert.Contains("Scion.Infrastructure.LocalClients", Layers.ReferencesOf(Layers.ClaudeCode));
        Assert.NotEmpty(Types.InAssembly(Layers.ClaudeCode).That().HaveDependencyOn(Layers.LocalClientsNamespace).GetTypes());
        Assert.Contains("Scion.Infrastructure.LocalClients", Layers.ReferencesOf(Layers.Codex));
        Assert.NotEmpty(Types.InAssembly(Layers.Codex).That().HaveDependencyOn(Layers.LocalClientsNamespace).GetTypes());
        Assert.NotEmpty(Types.InAssembly(Layers.Codex).That().HaveDependencyOn(Layers.ApplicationNamespace).GetTypes());
        Assert.Contains("Velopack", Layers.ReferencesOf(Layers.Velopack));
        Assert.NotEmpty(Types.InAssembly(Layers.Velopack).That().HaveDependencyOn("Velopack").GetTypes());
        Assert.NotEmpty(Types.InAssembly(Layers.Velopack).That().HaveDependencyOn(Layers.ApplicationNamespace).GetTypes());
    }
}
