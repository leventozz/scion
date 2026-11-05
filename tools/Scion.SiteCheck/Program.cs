namespace Scion.SiteCheck;

/// <summary>
/// <c>Scion.SiteCheck &lt;folder&gt;</c>: fails (exit code 1) when a page or stylesheet of the website points outside it,
/// except to the addresses in <see cref="AllowedAddresses.Site"/>. Exit code 2 is a wrong call or a missing folder.
/// </summary>
public static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out);

    public static int Run(string[] args, TextWriter output)
    {
        if (args.Length != 1)
        {
            output.WriteLine("Usage: Scion.SiteCheck <folder>");
            return 2;
        }

        try
        {
            var violations = SiteScan.Run(args[0], AllowedAddresses.Site);
            foreach (var violation in violations)
            {
                output.WriteLine($"{violation.File}: {violation.Address}");
            }

            if (violations.Count == 0)
            {
                output.WriteLine("The site has no external references.");
            }

            return violations.Count == 0 ? 0 : 1;
        }
        catch (DirectoryNotFoundException problem)
        {
            output.WriteLine(problem.Message);
            return 2;
        }
    }
}
