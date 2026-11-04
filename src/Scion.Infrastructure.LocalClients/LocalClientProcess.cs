using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Scion.Infrastructure.LocalClients;

/// <summary>
/// Runs a client program of the user (Claude Code, say) once. The arguments are passed as a list, never as a command line. The working
/// directory is a new empty folder, removed afterwards, so no project's settings, hooks or MCP files are ever loaded.
/// The environment is inherited exactly as it is: nothing is added and no credential is touched. The process is put under
/// <see cref="ChildProcessGuard"/>, so it does not outlive Scion.
/// </summary>
public static class LocalClientProcess
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc cref="RunAsync(string, Func{string, IReadOnlyList{string}}, string?, OutputHandling, CancellationToken)"/>
    public static Task<ProcessOutcome?> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? standardInput,
        OutputHandling output,
        CancellationToken cancellationToken) =>
        RunAsync(executable, _ => arguments, standardInput, output, cancellationToken);

    /// <param name="arguments">
    /// The arguments for the run, given the new working directory: a client that wants the folder it works in on its command
    /// line (<c>-C</c>) is handed the empty one it actually runs in.
    /// </param>
    /// <param name="standardInput">Written to the program and closed; null closes the input at once.</param>
    /// <param name="output">What is done with what the program prints.</param>
    /// <returns>The outcome, or null when the program could not be started.</returns>
    /// <exception cref="OperationCanceledException">After the process and its children have been ended.</exception>
    public static async Task<ProcessOutcome?> RunAsync(
        string executable,
        Func<string, IReadOnlyList<string>> arguments,
        string? standardInput,
        OutputHandling output,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var workingDirectory = Directory.CreateTempSubdirectory("scion-client-");
        try
        {
            using var process = Start(executable, arguments(workingDirectory.FullName), workingDirectory.FullName);
            if (process is null)
            {
                return null;
            }

            // From here on the system ends this process and its children with Scion, however Scion ends.
            ChildProcessGuard.ForThisApplication.Adopt(process);

            var standardOutput = output.ReadAsync(process.StandardOutput);
            var error = process.StandardError.ReadToEndAsync(CancellationToken.None);

            try
            {
                await WriteInputAsync(process, standardInput, cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                return new ProcessOutcome(process.ExitCode, await standardOutput, await error);
            }
            catch (OperationCanceledException)
            {
                await EndAsync(process);
                throw;
            }
        }
        finally
        {
            TryDelete(workingDirectory);
        }
    }

    private static Process? Start(string executable, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            return Process.Start(startInfo);
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task WriteInputAsync(Process process, string? standardInput, CancellationToken cancellationToken)
    {
        try
        {
            if (standardInput is not null)
            {
                await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
            }

            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The program ended before it read everything; its exit code tells what happened.
        }
    }

    private static async Task EndAsync(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
    }

    private static void TryDelete(DirectoryInfo directory)
    {
        try
        {
            directory.Delete(recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
