using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using Scion.Application.Adapting;
using Scion.Application.Creating;
using Scion.Application.Deleting;
using Scion.Application.Editing;
using Scion.Application.Projects;
using Scion.Application.Skills;
using Scion.Application.Skills.Copying;
using Scion.Application.Updating;
using Scion.Application.Viewing;
using Scion.Infrastructure.ClaudeCode;
using Scion.Infrastructure.Codex;
using Scion.Infrastructure.FileSystem;
using Scion.Infrastructure.ProjectRegistry;
using Scion.Infrastructure.Velopack;
using Velopack;
using Velopack.Locators;
using Scion.UI;

namespace Scion.Host.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // The updater starts Scion with special arguments after an install or an update; this must come before anything else.
        // It is called here, not from a helper in another assembly: vpk looks for this call in the entry assembly when it packs.
        VelopackApp.Build().Run();

        var builder = PhotinoBlazorApp.CreateBuilder(args);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Personal skills live under SCION_HOME when it is set, so tests never touch the real home directory.
        var personalRoot = ScionHome(userProfile);
        var claudeCode = new ClaudeCodeLayout();
        var codex = new CodexLayout();
        IAgentLayout[] layouts = [claudeCode, codex];
        IInstructionFileLayout[] instructionLayouts = [claudeCode, codex];
        builder.Services.AddSingleton<ISkillFolderReader>(new FileSystemSkillFolderReader(personalRoot, layouts));
        var skillCopier = new FileSystemSkillCopier(personalRoot, layouts);
        builder.Services.AddSingleton<ISkillFolderNames>(skillCopier);
        builder.Services.AddSingleton<ISkillFolderCopier>(skillCopier);
        builder.Services.AddSingleton<ISkillCopyLeftovers>(new FileSystemSkillCopyLeftovers(personalRoot, layouts));
        builder.Services.AddSingleton<ISkillFolderComparer>(new FileSystemSkillFolderComparer(personalRoot, layouts));
        builder.Services.AddSingleton<ISkillFiles>(new FileSystemSkillFiles(personalRoot, layouts));
        builder.Services.AddSingleton<IInstructionFiles>(new FileSystemInstructionFiles(personalRoot, instructionLayouts));
        var editableFiles = new FileSystemEditableFiles(personalRoot, layouts, instructionLayouts);
        builder.Services.AddSingleton<IEditableFileReader>(editableFiles);
        builder.Services.AddSingleton<IEditableFileWriter>(editableFiles);
        builder.Services.AddSingleton<IInstructionFileLeftovers>(new FileSystemInstructionFileLeftovers(personalRoot, instructionLayouts));
        builder.Services.AddSingleton<ISkillFrontmatterParser, YamlSkillFrontmatterParser>();
        builder.Services.AddSingleton<ISkillFrontmatterWriter, YamlSkillFrontmatterWriter>();
        builder.Services.AddSingleton<ISkillFolderCreator>(new FileSystemSkillCreator(personalRoot, layouts));
        builder.Services.AddSingleton<IInstructionFileCreator>(new FileSystemInstructionFileCreator(personalRoot, instructionLayouts));
        builder.Services.AddSingleton<ISkillTrash>(new FileSystemSkillTrash(ScionHome(userProfile), personalRoot, layouts));
        builder.Services.AddSingleton<ITargetContextReader>(new FileSystemTargetContext(personalRoot, instructionLayouts));
        builder.Services.AddSingleton<ISkillNameSetter, YamlSkillNameSetter>();
        builder.Services.AddSingleton<IAdaptedSkillWriter>(new FileSystemAdaptedSkillWriter(personalRoot, layouts));
        AddAiProviders(builder.Services, userProfile, personalRoot);
        AddUpdates(builder.Services, ScionHome(userProfile));
        var pathComparison = PathComparisons.For(CurrentPlatform());
        builder.Services.AddSingleton<IFileBackups>(
            new FileSystemFileBackups(ScionHome(userProfile), personalRoot, layouts, instructionLayouts, pathComparison));
        builder.Services.AddSingleton(new PathComparison(pathComparison));
        var projectRegistry = new JsonProjectRegistry(ScionHome(userProfile), pathComparison);
        builder.Services.AddSingleton<IProjectRegistry>(projectRegistry);
        builder.Services.AddSingleton<ILastUsedAiProvider>(new JsonLastUsedAiProvider(ScionHome(userProfile)));
        builder.Services.AddSingleton<IProjectRegistryRecovery>(projectRegistry);
        builder.Services.AddSingleton<IFolderProbe, FileSystemFolderProbe>();
        builder.Services.AddSingleton(new UserHome(userProfile, pathComparison));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ListSkills>();
        builder.Services.AddSingleton<ListProjects>();
        builder.Services.AddSingleton<RegisterProject>();
        builder.Services.AddSingleton<OpenProject>();
        builder.Services.AddSingleton<RenameProject>();
        builder.Services.AddSingleton<RemoveProject>();
        builder.Services.AddSingleton<StartFresh>();
        builder.Services.AddSingleton<PreviewSkillCopy>();
        builder.Services.AddSingleton<CopySkills>();
        builder.Services.AddSingleton<StartupLocations>();
        builder.Services.AddSingleton<CleanUpCopyLeftovers>();
        builder.Services.AddSingleton<CleanUpEditLeftovers>();
        builder.Services.AddSingleton<ListSkillFiles>();
        builder.Services.AddSingleton<ReadSkillFile>();
        builder.Services.AddSingleton<ReadInstructionFiles>();
        builder.Services.AddSingleton<OpenFileForEditing>();
        builder.Services.AddSingleton<SaveEditedFile>();
        builder.Services.AddSingleton<CheckSkillFrontmatter>();
        builder.Services.AddSingleton<PrepareNewSkill>();
        builder.Services.AddSingleton<CreateSkill>();
        builder.Services.AddSingleton<CreateInstructionFile>();
        builder.Services.AddSingleton<DeleteSkill>();
        builder.Services.AddSingleton(typeof(IAiProviderPorts<>), typeof(KeyedAiProviderPorts<>));
        builder.Services.AddSingleton<CheckAiProvider>();
        builder.Services.AddSingleton<CheckAiUsageBasis>();
        builder.Services.AddSingleton<ListProviderOwnFiles>();
        builder.Services.AddSingleton<ChooseAiProvider>();
        builder.Services.AddSingleton<RememberAiProvider>();
        builder.Services.AddSingleton<SignInToAiProvider>();
        builder.Services.AddSingleton<PrepareAdaptation>();
        builder.Services.AddSingleton<AdaptSkill>();
        builder.Services.AddSingleton<SaveAdaptedSkill>();
        AddShell(builder.Services);
        PhotinoBlazorApp? app = null;
        builder.Services.AddSingleton<IWindowCloser>(new PhotinoWindowCloser(() => app!.MainWindow));
        builder.Services.AddSingleton<CloseGuard>();
        AddFolderPicker(builder.Services, userProfile, () => app!.MainWindow);

        builder.RootComponents.Add<App>("#app");
        builder.ConfigureMainWindow(window =>
        {
            window.SetTitle("Scion").SetSize(1100, 720);

            if (OperatingSystem.IsWindows())
            {
                window.SetIconFile(Path.Combine(AppContext.BaseDirectory, "Assets", "scion.ico"));
            }
        });

        using var builtApp = builder.Build();
        app = builtApp;

        // A window with an unsaved draft is not closed until the user decided what to do with the draft.
        var closeGuard = builtApp.Services.GetRequiredService<CloseGuard>();
        builtApp.MainWindow.RegisterClosingHandler((_, closing) => closing.Cancel = closeGuard.TryHoldClose());

        // Clears what an interrupted copy or save left behind; best effort, so it must not hold up the window.
        _ = Task.Run(() => builtApp.Services.GetRequiredService<CleanUpCopyLeftovers>().ExecuteAsync());
        _ = Task.Run(() => builtApp.Services.GetRequiredService<CleanUpEditLeftovers>().ExecuteAsync());

        return builtApp.Run();
    }

    private static string ScionHome(string userProfile) =>
        Environment.GetEnvironmentVariable("SCION_HOME") is { Length: > 0 } scionHome ? scionHome : userProfile;

    private static OSPlatform CurrentPlatform() =>
        new[] { OSPlatform.Windows, OSPlatform.OSX, OSPlatform.Linux, OSPlatform.FreeBSD }
            .FirstOrDefault(RuntimeInformation.IsOSPlatform);

    /// <summary>
    /// The providers, in the order the user is offered them. Each one's ports are registered under its id: the use cases find them
    /// through <see cref="KeyedAiProviderPorts{TPort}"/>. A new provider is a method like <see cref="AddClaudeCode"/>, called from
    /// here, and its provider in the catalog.
    /// </summary>
    private static void AddAiProviders(IServiceCollection services, string userProfile, string personalRoot)
    {
        AddClaudeCode(services, userProfile);
        AddCodex(services, userProfile, personalRoot);
        services.AddSingleton(new AiProviderCatalog([ClaudeCodeProvider.Claude, CodexProvider.Codex]));
    }

    /// <summary>Claude is reached only through the user's own Claude Code; SCION_CLAUDE_PATH points the tests at a stand-in.</summary>
    private static void AddClaudeCode(IServiceCollection services, string userProfile)
    {
        var executable = new ClaudeCodeExecutable(
            Environment.GetEnvironmentVariable("SCION_CLAUDE_PATH"), userProfile, name => Environment.GetEnvironmentVariable(name));
        var status = new ClaudeCodeStatus(executable);

        var id = ClaudeCodeProvider.Claude.Id;

        services.AddKeyedSingleton<IAiProviderStatus>(id, status);
        services.AddKeyedSingleton<IAiUsageBasisReader>(id, status);
        services.AddKeyedSingleton<ISkillAdapter>(id, new ClaudeCodeSkillAdapter(executable, status));
        services.AddKeyedSingleton<IAiProviderOwnFiles>(id, new ClaudeCodeOwnFiles());
    }

    /// <summary>
    /// Codex is reached only through the user's own Codex CLI; SCION_CODEX_PATH points the tests at a stand-in. The file Codex
    /// loads by itself is looked for under the same root as the personal skills, so tests never see the real home directory.
    /// </summary>
    private static void AddCodex(IServiceCollection services, string userProfile, string personalRoot)
    {
        var executable = new CodexExecutable(
            Environment.GetEnvironmentVariable("SCION_CODEX_PATH"), userProfile, name => Environment.GetEnvironmentVariable(name));
        var status = new CodexStatus(executable);

        var id = CodexProvider.Codex.Id;

        services.AddKeyedSingleton<IAiProviderStatus>(id, status);
        services.AddKeyedSingleton<IAiUsageBasisReader>(id, status);
        services.AddKeyedSingleton<ISkillAdapter>(id, new CodexSkillAdapter(executable, status));
        services.AddKeyedSingleton<IAiProviderOwnFiles>(id, new CodexOwnFiles(personalRoot));
    }

    /// <summary>
    /// Scion updates itself from the releases of its GitHub repository; SCION_UPDATE_SOURCE names another repository address or a
    /// folder of releases, which is how an update is tried without touching GitHub. SCION_TEST_INSTALLED_VERSION makes Scion
    /// behave as if that version was installed (a fake installation under the scion folder), so an update can be tried from a build.
    /// </summary>
    private static void AddUpdates(IServiceCollection services, string scionHome)
    {
        var source = Environment.GetEnvironmentVariable("SCION_UPDATE_SOURCE") is { Length: > 0 } named
            ? named
            : "https://github.com/leventozz/scion";
        var installation = Environment.GetEnvironmentVariable("SCION_TEST_INSTALLED_VERSION") is { Length: > 0 } installedVersion
            ? new TestVelopackLocator("Scion", installedVersion, Path.Combine(scionHome, ".scion", "test-packages"))
            : null;
        var updates = new VelopackUpdates(source, BuildVersion(), installation);
        var updatesFile = new JsonUpdatesFile(scionHome);

        services.AddSingleton<IUpdateFeed>(updates);
        services.AddSingleton<IUpdateDownloader>(updates);
        services.AddSingleton<IUpdateApplier>(updates);
        services.AddSingleton<IScionInstallation>(updates);
        services.AddSingleton<IUpdateSettings>(updatesFile);
        services.AddSingleton<IUpdateCheckRecord>(updatesFile);
        services.AddSingleton<CheckForUpdates>();
        services.AddSingleton<DownloadUpdate>();
        services.AddSingleton<ApplyUpdate>();
        services.AddSingleton<ReadUpdateSettings>();
        services.AddSingleton<ChangeUpdateSettings>();
        services.AddSingleton<UpdateSession>();
        services.AddSingleton(new UpdateLinks(new Uri("https://github.com/leventozz/scion/releases")));
    }

    /// <summary>The version this build was made with: what a Scion that is not installed tells as its version.</summary>
    private static string BuildVersion() =>
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    private static void AddShell(IServiceCollection services)
    {
        if (Environment.GetEnvironmentVariable("SCION_TEST_SHELL_LOG") is { Length: > 0 } shellLog)
        {
            var recordingShell = new RecordingShell(shellLog);
            services.AddSingleton<IFolderRevealer>(recordingShell);
            services.AddSingleton<IExternalLinks>(recordingShell);
            return;
        }

        services.AddSingleton<IFolderRevealer, ShellFolderRevealer>();
        services.AddSingleton<IExternalLinks, ShellExternalLinks>();
    }

    private static void AddFolderPicker(
        IServiceCollection services, string userProfile, Func<Photino.NET.PhotinoWindow> mainWindow)
    {
        if (Environment.GetEnvironmentVariable("SCION_TEST_FOLDER") is { Length: > 0 } testFolder)
        {
            services.AddSingleton<IFolderPicker>(new FixedFolderPicker(testFolder));
            return;
        }

        services.AddSingleton<IFolderPicker>(new PhotinoFolderPicker(mainWindow, PickerStartDirectory(userProfile)));
    }

    private static string PickerStartDirectory(string userProfile)
    {
        var repositories = Path.Combine(userProfile, "source", "repos");

        return OperatingSystem.IsWindows() && Directory.Exists(repositories) ? repositories : userProfile;
    }
}
