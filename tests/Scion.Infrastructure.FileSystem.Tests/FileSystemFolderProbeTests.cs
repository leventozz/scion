namespace Scion.Infrastructure.FileSystem.Tests;

public class FileSystemFolderProbeTests
{
    [Fact]
    public void Finds_an_existing_folder()
    {
        using var folder = new TempDirectory();

        Assert.True(new FileSystemFolderProbe().Exists(folder.Path));
    }

    [Fact]
    public void Does_not_find_a_folder_that_was_deleted()
    {
        var folder = new TempDirectory();
        var path = folder.Path;
        folder.Dispose();

        Assert.False(new FileSystemFolderProbe().Exists(path));
    }

    [Fact]
    public void Does_not_count_a_file_as_a_folder()
    {
        using var folder = new TempDirectory();
        folder.WriteFile("notes.txt", "text");

        Assert.False(new FileSystemFolderProbe().Exists(System.IO.Path.Combine(folder.Path, "notes.txt")));
    }
}
