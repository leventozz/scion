namespace Scion.UI.Tests;

internal sealed class FakeFolderRevealer : IFolderRevealer
{
    public List<string> Shown { get; } = [];

    public Task ShowAsync(string folderPath)
    {
        Shown.Add(folderPath);

        return Task.CompletedTask;
    }
}

internal sealed class FakeExternalLinks : IExternalLinks
{
    public List<Uri> Opened { get; } = [];

    public Task OpenAsync(Uri uri)
    {
        Opened.Add(uri);

        return Task.CompletedTask;
    }
}
