using Scion.Application.Projects;
using Scion.Domain;

namespace Scion.Infrastructure.ProjectRegistry.Tests;

public class JsonProjectRegistryTests : IDisposable
{
    private readonly TempDirectory _scionHome = new();

    public void Dispose() => _scionHome.Dispose();

    [Fact]
    public async Task Lists_nothing_when_the_registry_file_does_not_exist()
    {
        var registry = CreateRegistry();

        Assert.Empty(await registry.ListAsync(CancellationToken.None));
        Assert.Null(await registry.FindByPathAsync("/work/api", CancellationToken.None));
    }

    [Fact]
    public async Task Save_creates_the_registry_file_under_the_scion_folder_of_the_scion_home()
    {
        await CreateRegistry().SaveAsync(Project("/work/api"), CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(_scionHome.Path, ".scion", "projects.json")));
    }

    [Fact]
    public async Task Round_trips_path_display_name_and_last_opened_time_including_its_offset()
    {
        var project = new RegisteredProject(
            "/work/api", "Our API", new DateTimeOffset(2026, 3, 5, 12, 30, 15, TimeSpan.FromHours(3)));
        await CreateRegistry().SaveAsync(project, CancellationToken.None);

        var listed = Assert.Single(await CreateRegistry().ListAsync(CancellationToken.None));

        Assert.Equal(project, listed);
        Assert.Equal(TimeSpan.FromHours(3), listed.LastOpenedAt.Offset);
    }

    [Fact]
    public async Task Finds_a_saved_project_by_its_path()
    {
        var registry = CreateRegistry();
        await registry.SaveAsync(Project("/work/api"), CancellationToken.None);
        await registry.SaveAsync(Project("/work/web"), CancellationToken.None);

        var found = await registry.FindByPathAsync("/work/web", CancellationToken.None);

        Assert.Equal("/work/web", found!.Path);
    }

    [Fact]
    public async Task Saving_a_project_with_the_same_path_replaces_the_entry()
    {
        var registry = CreateRegistry();
        await registry.SaveAsync(Project("/work/api", "old name"), CancellationToken.None);

        await registry.SaveAsync(Project("/work/api", "new name"), CancellationToken.None);

        var listed = Assert.Single(await registry.ListAsync(CancellationToken.None));
        Assert.Equal("new name", listed.DisplayName);
    }

    [Theory]
    [InlineData("/work/api/")]
    [InlineData(@"/work/api\")]
    public async Task Treats_a_path_with_a_trailing_separator_as_the_same_project(string pathWithSeparator)
    {
        var registry = CreateRegistry();
        await registry.SaveAsync(Project("/work/api", "first"), CancellationToken.None);

        await registry.SaveAsync(Project(pathWithSeparator, "second"), CancellationToken.None);

        var listed = Assert.Single(await registry.ListAsync(CancellationToken.None));
        Assert.Equal("second", listed.DisplayName);
        Assert.Equal("/work/api", (await registry.FindByPathAsync(pathWithSeparator, CancellationToken.None))!.Path);
    }

    [Fact]
    public async Task Stores_the_path_without_a_trailing_separator()
    {
        await CreateRegistry().SaveAsync(Project("/work/api/"), CancellationToken.None);

        var listed = Assert.Single(await CreateRegistry().ListAsync(CancellationToken.None));

        Assert.Equal("/work/api", listed.Path);
    }

    [Fact]
    public async Task Keeps_a_root_path_as_it_is()
    {
        await CreateRegistry().SaveAsync(Project("/"), CancellationToken.None);

        var listed = Assert.Single(await CreateRegistry().ListAsync(CancellationToken.None));

        Assert.Equal("/", listed.Path);
    }

    [Fact]
    public async Task Matches_paths_that_differ_only_in_case_when_comparing_ignoring_case()
    {
        var registry = CreateRegistry(StringComparison.OrdinalIgnoreCase);
        await registry.SaveAsync(Project("/Work/Api"), CancellationToken.None);

        await registry.SaveAsync(Project("/work/api"), CancellationToken.None);

        Assert.Single(await registry.ListAsync(CancellationToken.None));
        Assert.NotNull(await registry.FindByPathAsync("/WORK/API", CancellationToken.None));
    }

    [Fact]
    public async Task Keeps_paths_that_differ_in_case_apart_when_comparing_case_sensitively()
    {
        var registry = CreateRegistry(StringComparison.Ordinal);
        await registry.SaveAsync(Project("/Work/Api"), CancellationToken.None);

        await registry.SaveAsync(Project("/work/api"), CancellationToken.None);

        Assert.Equal(2, (await registry.ListAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Leaves_no_temporary_file_behind_after_saving()
    {
        var registry = CreateRegistry();
        await registry.SaveAsync(Project("/work/api"), CancellationToken.None);
        await registry.SaveAsync(Project("/work/web"), CancellationToken.None);

        var files = Directory.GetFiles(Path.Combine(_scionHome.Path, ".scion")).Select(Path.GetFileName);

        Assert.Equal(["projects.json"], files);
    }

    [Fact]
    public async Task Keeps_the_previous_registry_content_when_writing_the_new_content_fails()
    {
        var registry = CreateRegistry();
        await registry.SaveAsync(Project("/work/api"), CancellationToken.None);
        var registryFile = Path.Combine(_scionHome.Path, ".scion", "projects.json");
        var contentBefore = File.ReadAllBytes(registryFile);
        Directory.CreateDirectory(registryFile + ".tmp");

        var exception = await Assert.ThrowsAsync<ProjectRegistryWriteFailedException>(
            () => registry.SaveAsync(Project("/work/web"), CancellationToken.None));
        Assert.Equal(registryFile, exception.RegistryFile);
        Assert.Equal(contentBefore, File.ReadAllBytes(registryFile));
    }

    [Fact]
    public async Task Listing_throws_unreadable_with_the_registry_file_when_the_file_cannot_be_opened()
    {
        var registryFile = WriteRegistryFile("{\"projects\":[]}");
        using var lockedFile = new FileStream(registryFile, FileMode.Open, FileAccess.Read, FileShare.None);

        var exception = await Assert.ThrowsAsync<ProjectRegistryUnreadableException>(
            () => CreateRegistry().ListAsync(CancellationToken.None));
        Assert.Equal(registryFile, exception.RegistryFile);
    }

    [Fact]
    public async Task Remove_unregisters_only_the_project_with_that_path()
    {
        var registry = CreateRegistry();
        await registry.SaveAsync(Project("/work/api"), CancellationToken.None);
        await registry.SaveAsync(Project("/work/web"), CancellationToken.None);

        await registry.RemoveAsync("/work/api", CancellationToken.None);

        Assert.Equal(["/work/web"], (await registry.ListAsync(CancellationToken.None)).Select(project => project.Path));
    }

    [Fact]
    public async Task Remove_uses_the_same_path_rule_as_save()
    {
        var registry = CreateRegistry(StringComparison.OrdinalIgnoreCase);
        await registry.SaveAsync(Project("/Work/Api/"), CancellationToken.None);

        await registry.RemoveAsync("/work/api", CancellationToken.None);

        Assert.Empty(await registry.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Remove_of_an_unknown_path_changes_nothing()
    {
        var registry = CreateRegistry();
        await registry.SaveAsync(Project("/work/api"), CancellationToken.None);
        var registryFile = Path.Combine(_scionHome.Path, ".scion", "projects.json");
        var contentBefore = File.ReadAllBytes(registryFile);

        await registry.RemoveAsync("/work/unknown", CancellationToken.None);

        Assert.Equal(contentBefore, File.ReadAllBytes(registryFile));
    }

    [Fact]
    public async Task Remove_without_a_registry_file_creates_nothing()
    {
        await CreateRegistry().RemoveAsync("/work/api", CancellationToken.None);

        Assert.False(Directory.Exists(Path.Combine(_scionHome.Path, ".scion")));
    }

    [Fact]
    public async Task Remove_never_touches_the_project_folder_on_disk()
    {
        using var projectFolder = new TempDirectory();
        var projectFile = Path.Combine(projectFolder.Path, "CLAUDE.md");
        File.WriteAllText(projectFile, "# Instructions");
        var registry = CreateRegistry();
        await registry.SaveAsync(Project(projectFolder.Path), CancellationToken.None);

        await registry.RemoveAsync(projectFolder.Path, CancellationToken.None);

        Assert.True(File.Exists(projectFile));
    }

    [Fact]
    public async Task Remove_throws_unreadable_for_an_unreadable_registry_file_and_leaves_it_untouched()
    {
        var registryFile = WriteRegistryFile("{ this is not json");
        var bytesBefore = File.ReadAllBytes(registryFile);

        await Assert.ThrowsAsync<ProjectRegistryUnreadableException>(
            () => CreateRegistry().RemoveAsync("/work/api", CancellationToken.None));
        Assert.Equal(bytesBefore, File.ReadAllBytes(registryFile));
    }

    [Fact]
    public async Task Setting_the_registry_aside_keeps_the_original_bytes_in_a_timestamped_backup()
    {
        var registryFile = WriteRegistryFile("{ this is not json");
        var bytesBefore = File.ReadAllBytes(registryFile);
        var now = new DateTimeOffset(2026, 5, 1, 13, 4, 5, TimeSpan.FromHours(3));

        var backup = await CreateRegistry().SetAsideAsync(now, CancellationToken.None);

        Assert.Equal(registryFile + ".corrupt-20260501T100405Z", backup);
        Assert.Equal(bytesBefore, File.ReadAllBytes(backup!));
        Assert.False(File.Exists(registryFile));
    }

    [Fact]
    public async Task Lists_an_empty_registry_after_setting_the_unreadable_one_aside()
    {
        WriteRegistryFile("{ this is not json");
        var registry = CreateRegistry();

        await registry.SetAsideAsync(DateTimeOffset.UnixEpoch, CancellationToken.None);

        Assert.Empty(await registry.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Never_overwrites_an_existing_backup_and_numbers_the_new_one_instead()
    {
        var registryFile = WriteRegistryFile("second");
        var now = DateTimeOffset.UnixEpoch;
        File.WriteAllText(registryFile + ".corrupt-19700101T000000Z", "first");
        File.WriteAllText(registryFile + ".corrupt-19700101T000000Z-2", "other");

        var backup = await CreateRegistry().SetAsideAsync(now, CancellationToken.None);

        Assert.Equal(registryFile + ".corrupt-19700101T000000Z-3", backup);
        Assert.Equal("second", File.ReadAllText(backup!));
        Assert.Equal("first", File.ReadAllText(registryFile + ".corrupt-19700101T000000Z"));
        Assert.Equal("other", File.ReadAllText(registryFile + ".corrupt-19700101T000000Z-2"));
    }

    [Fact]
    public async Task Setting_a_missing_registry_aside_succeeds_and_has_no_backup()
    {
        var backup = await CreateRegistry().SetAsideAsync(DateTimeOffset.UnixEpoch, CancellationToken.None);

        Assert.Null(backup);
    }

    [Fact]
    public async Task Setting_the_registry_aside_throws_write_failed_when_the_file_cannot_be_moved()
    {
        var registryFile = WriteRegistryFile("content");
        Directory.CreateDirectory(registryFile + ".corrupt-19700101T000000Z");

        var exception = await Assert.ThrowsAsync<ProjectRegistryWriteFailedException>(
            () => CreateRegistry().SetAsideAsync(DateTimeOffset.UnixEpoch, CancellationToken.None));
        Assert.Equal(registryFile, exception.RegistryFile);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("   \n")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"projects\":null}")]
    public async Task Listing_an_unreadable_registry_file_throws_and_leaves_the_file_untouched(string content)
    {
        var registryFile = WriteRegistryFile(content);
        var bytesBefore = File.ReadAllBytes(registryFile);

        await Assert.ThrowsAsync<ProjectRegistryUnreadableException>(
            () => CreateRegistry().ListAsync(CancellationToken.None));
        Assert.Equal(bytesBefore, File.ReadAllBytes(registryFile));
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("   \n")]
    public async Task Saving_into_an_unreadable_registry_file_throws_before_writing_anything(string content)
    {
        var registryFile = WriteRegistryFile(content);
        var bytesBefore = File.ReadAllBytes(registryFile);

        await Assert.ThrowsAsync<ProjectRegistryUnreadableException>(
            () => CreateRegistry().SaveAsync(Project("/work/api"), CancellationToken.None));
        Assert.Equal(bytesBefore, File.ReadAllBytes(registryFile));
        Assert.Equal([registryFile], Directory.GetFiles(Path.GetDirectoryName(registryFile)!));
    }

    [Fact]
    public async Task Finding_in_an_unreadable_registry_file_throws()
    {
        WriteRegistryFile("{ this is not json");

        await Assert.ThrowsAsync<ProjectRegistryUnreadableException>(
            () => CreateRegistry().FindByPathAsync("/work/api", CancellationToken.None));
    }

    private string WriteRegistryFile(string content)
    {
        var registryFile = Path.Combine(_scionHome.Path, ".scion", "projects.json");
        Directory.CreateDirectory(Path.GetDirectoryName(registryFile)!);
        File.WriteAllText(registryFile, content);

        return registryFile;
    }

    private JsonProjectRegistry CreateRegistry(StringComparison pathComparison = StringComparison.Ordinal) =>
        new(_scionHome.Path, pathComparison);

    private static RegisteredProject Project(string path, string displayName = "project") =>
        new(path, displayName, new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero));
}
