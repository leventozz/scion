namespace Scion.UI.Tests;

public class DisplayPathTests
{
    private static readonly UserHome WindowsHome = new(@"C:\Users\me", StringComparison.OrdinalIgnoreCase);
    private static readonly UserHome PosixHome = new("/home/me", StringComparison.Ordinal);

    [Theory]
    [InlineData(@"C:\Users\me", "~")]
    [InlineData(@"C:\Users\me\work\api", @"~\work\api")]
    [InlineData(@"c:\users\ME\Docs", @"~\Docs")]
    [InlineData(@"C:\Users\me2\source", @"C:\Users\me2\source")]
    [InlineData(@"D:\work\api", @"D:\work\api")]
    public void Abbreviates_the_home_directory_on_windows_paths(string path, string expected)
    {
        Assert.Equal(expected, DisplayPath.Abbreviate(path, WindowsHome));
    }

    [Theory]
    [InlineData("/home/me", "~")]
    [InlineData("/home/me/work/api", "~/work/api")]
    [InlineData("/home/meow/work", "/home/meow/work")]
    [InlineData("/work/api", "/work/api")]
    [InlineData("/HOME/me/work", "/HOME/me/work")]
    public void Abbreviates_the_home_directory_on_posix_paths_and_respects_case_there(string path, string expected)
    {
        Assert.Equal(expected, DisplayPath.Abbreviate(path, PosixHome));
    }

    [Fact]
    public void Ignores_a_trailing_separator_on_the_home_directory()
    {
        var home = new UserHome("/home/me/", StringComparison.Ordinal);

        Assert.Equal("~/work", DisplayPath.Abbreviate("/home/me/work", home));
    }

    [Theory]
    [InlineData("~/work/api", "~/work/", "api")]
    [InlineData(@"~\work\api", @"~\work\", "api")]
    [InlineData("/work/api", "/work/", "api")]
    [InlineData("~", "", "~")]
    [InlineData("api", "", "api")]
    [InlineData("/", "/", "")]
    public void Splits_a_path_into_its_head_with_the_separator_and_its_last_folder(
        string path, string expectedHead, string expectedLastFolder)
    {
        var (head, lastFolder) = DisplayPath.Split(path);

        Assert.Equal(expectedHead, head);
        Assert.Equal(expectedLastFolder, lastFolder);
    }
}
