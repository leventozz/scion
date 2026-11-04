using Microsoft.Extensions.DependencyInjection;
using Scion.Application.Adapting;
using Scion.Application.Creating;
using Scion.Application.Deleting;
using Scion.Application.Editing;
using Scion.Application.Projects;
using Scion.Application.Skills;
using Scion.Application.Skills.Copying;
using Scion.Application.Updating;
using Scion.Application.Viewing;

namespace Scion.UI.Tests;

/// <summary>The ports of the UI as in-memory fakes, wired to the real use cases.</summary>
internal sealed class UiFakes
{
    public static readonly DateTimeOffset Noon = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    public const string HomeDirectory = "/home/me";

    public UiFakes()
    {
        Recovery = new InMemoryProjectRegistryRecovery(Registry);
        SkillFolders = new InMemorySkillFolders(Reader);
        EditableFiles = new InMemoryEditableFiles(SkillFiles, InstructionFiles);
        CloseGuard = new CloseGuard(WindowCloser);
        SkillCreation = new InMemorySkillCreation(Reader, SkillFiles);
        InstructionFileCreation = new InMemoryInstructionFileCreation(InstructionFiles);
        SkillTrash = new InMemorySkillTrash(Reader);
        AdaptedSkillWriter = new InMemoryAdaptedSkillWriter(Reader);
        var check = new CheckForUpdates(UpdateFeed, UpdateSettings, UpdateCheckRecord, Installation, Time);
        UpdateSession = new UpdateSession(check, new DownloadUpdate(UpdateDownloader), new ApplyUpdate(UpdateApplier), Installation, CloseGuard);
    }

    public InMemoryProjectRegistry Registry { get; } = new();

    public InMemorySkillFolderReader Reader { get; } = new();

    public InMemoryFolderProbe Folders { get; } = new();

    public InMemorySkillFolders SkillFolders { get; }

    public FakeFolderPicker Picker { get; } = new();

    public InMemoryProjectRegistryRecovery Recovery { get; }

    public InMemorySkillFiles SkillFiles { get; } = new();

    public InMemoryInstructionFiles InstructionFiles { get; } = new();

    public InMemoryEditableFiles EditableFiles { get; }

    public InMemorySkillCreation SkillCreation { get; }

    public InMemoryInstructionFileCreation InstructionFileCreation { get; }

    public InMemorySkillTrash SkillTrash { get; }

    public FakeSkillAdapter SkillAdapter { get; } = new();

    public FakeAiProviderStatus ProviderStatus { get; } = new();

    public InMemoryTargetContext TargetContext { get; } = new();

    public InMemoryAdaptedSkillWriter AdaptedSkillWriter { get; }

    public FakeOwnFiles OwnFiles { get; } = new();

    public FakeLastUsedAiProvider LastUsed { get; } = new();

    public static AiProvider Provider { get; } = new(new AiProviderId("claude"), "Claude", "Claude Code", "Claude",
        new Uri("https://example.test/install-claude-code"),
        new AiUsageNotes("Counts toward your Claude plan.", "Uses your Anthropic API key."));

    /// <summary>The second provider of the catalog in the tests that offer two (see <see cref="AddBothProvidersTo"/>).</summary>
    public static AiProvider CodexProvider { get; } = new(new AiProviderId("codex"), "Codex", "Codex CLI", "ChatGPT",
        new Uri("https://example.test/install-codex"),
        AiUsageNotes.Always("Counts toward your ChatGPT plan unless an API key is set for Codex."));

    public FakeAiProviderStatus CodexStatus { get; } = new();

    public FakeSkillAdapter CodexAdapter { get; } = new();

    public FakeOwnFiles CodexOwnFiles { get; } = new();

    public RecordingBackups Backups { get; } = new();

    public FakeFolderRevealer Revealer { get; } = new();

    public FakeExternalLinks Links { get; } = new();

    public FixedTimeProvider Time { get; } = new(Noon);

    public FakeFrontmatterParser FrontmatterParser { get; } = new();

    public RecordingWindowCloser WindowCloser { get; } = new();

    public static Uri ReleasesPage { get; } = new("https://example.test/releases");

    public FakeScionInstallation Installation { get; } = new();

    public FakeUpdateFeed UpdateFeed { get; } = new();

    public FakeUpdateDownloader UpdateDownloader { get; } = new();

    public RecordingUpdateApplier UpdateApplier { get; } = new();

    public FakeUpdateSettings UpdateSettings { get; } = new();

    public FakeUpdateCheckRecord UpdateCheckRecord { get; } = new();

    public UpdateSession UpdateSession { get; }

    public CloseGuard CloseGuard { get; }

    /// <summary>The ports of one provider as a test sets them up.</summary>
    public sealed record ProviderSetup(AiProvider Provider, FakeAiProviderStatus Status, FakeSkillAdapter Adapter, FakeOwnFiles OwnFiles);

    /// <summary>Offers one provider in the catalog, with the ports of that provider only. Registering again replaces the earlier one.</summary>
    public void AddProviderTo(IServiceCollection services, AiProvider provider, FakeAiProviderStatus status, FakeSkillAdapter adapter) =>
        AddProvidersTo(services, new ProviderSetup(provider, status, adapter, new FakeOwnFiles()));

    /// <summary>Offers Claude and then Codex, with the fakes of this object.</summary>
    public void AddBothProvidersTo(IServiceCollection services) =>
        AddProvidersTo(
            services,
            new ProviderSetup(Provider, ProviderStatus, SkillAdapter, OwnFiles),
            new ProviderSetup(CodexProvider, CodexStatus, CodexAdapter, CodexOwnFiles));

    /// <summary>Offers these providers in the catalog, in this order, each with its own ports. Registering again replaces the earlier ones.</summary>
    public void AddProvidersTo(IServiceCollection services, params ProviderSetup[] setups)
    {
        var statuses = new ProviderPorts<IAiProviderStatus>();
        var readers = new ProviderPorts<IAiUsageBasisReader>();
        var adapters = new ProviderPorts<ISkillAdapter>();
        var ownFiles = new ProviderPorts<IAiProviderOwnFiles>();
        foreach (var setup in setups)
        {
            statuses.With(setup.Provider.Id, setup.Status);
            readers.With(setup.Provider.Id, setup.Status);
            adapters.With(setup.Provider.Id, setup.Adapter);
            ownFiles.With(setup.Provider.Id, setup.OwnFiles);
        }

        var catalog = new AiProviderCatalog([.. setups.Select(setup => setup.Provider)]);
        services.AddSingleton(catalog);
        services.AddSingleton(new CheckAiProvider(statuses));
        services.AddSingleton(new CheckAiUsageBasis(readers));
        services.AddSingleton(new SignInToAiProvider(statuses, Time));
        services.AddSingleton(new ListProviderOwnFiles(ownFiles));
        services.AddSingleton(new ChooseAiProvider(catalog, LastUsed));
        services.AddSingleton(new RememberAiProvider(LastUsed));
        services.AddSingleton(new AdaptSkill(
            adapters,
            new FakeSkillNameSetter(),
            new CheckSkillFrontmatter(FrontmatterParser),
            SkillFolders,
            new PathComparison(StringComparison.Ordinal),
            Time));
    }

    public void AddTo(IServiceCollection services)
    {
        var listSkills = new ListSkills(Reader);

        services.AddSingleton<TimeProvider>(Time);
        services.AddSingleton(CloseGuard);
        services.AddSingleton(listSkills);
        services.AddSingleton(new ListProjects(Registry, listSkills, Folders));
        services.AddSingleton(new RegisterProject(Registry, Time));
        services.AddSingleton(new OpenProject(Registry, Time, Folders));
        services.AddSingleton(new RemoveProject(Registry));
        services.AddSingleton(new RenameProject(Registry));
        services.AddSingleton(new StartFresh(Recovery, Time));
        var pathComparison = new PathComparison(StringComparison.Ordinal);
        services.AddSingleton(pathComparison);
        services.AddSingleton(new PreviewSkillCopy(Folders, SkillFolders, SkillFolders, pathComparison));
        services.AddSingleton(new CopySkills(Folders, SkillFolders, SkillFolders, SkillFolders, pathComparison));
        services.AddSingleton(new PrepareNewSkill(SkillFolders, pathComparison));
        services.AddSingleton(new CreateSkill(Folders, SkillFolders, new FakeFrontmatterWriter(), SkillCreation, pathComparison));
        services.AddSingleton(new CreateInstructionFile(InstructionFileCreation));
        services.AddSingleton(new DeleteSkill(SkillTrash, Time));
        services.AddSingleton<IScionInstallation>(Installation);
        services.AddSingleton(new ReadUpdateSettings(UpdateSettings));
        services.AddSingleton(new ChangeUpdateSettings(UpdateSettings));
        services.AddSingleton(UpdateSession);
        services.AddSingleton(new UpdateLinks(ReleasesPage));
        services.AddSingleton(new ListSkillFiles(SkillFiles));
        services.AddSingleton(new ReadSkillFile(SkillFiles));
        services.AddSingleton(new ReadInstructionFiles(InstructionFiles));
        services.AddSingleton(new OpenFileForEditing(EditableFiles));
        services.AddSingleton(new SaveEditedFile(EditableFiles, EditableFiles, Backups, Time));
        var checkSkillFrontmatter = new CheckSkillFrontmatter(FrontmatterParser);
        services.AddSingleton(checkSkillFrontmatter);
        AddProviderTo(services, Provider, ProviderStatus, SkillAdapter);
        services.AddSingleton(new PrepareAdaptation(
            new OpenFileForEditing(EditableFiles), TargetContext, FrontmatterParser, Folders, pathComparison));
        services.AddSingleton(new SaveAdaptedSkill(SkillFolders, AdaptedSkillWriter, Folders, pathComparison));
        services.AddSingleton<IFolderRevealer>(Revealer);
        services.AddSingleton<IExternalLinks>(Links);
        services.AddSingleton<IFolderPicker>(Picker);
        services.AddSingleton(new UserHome(HomeDirectory, StringComparison.Ordinal));
    }
}
