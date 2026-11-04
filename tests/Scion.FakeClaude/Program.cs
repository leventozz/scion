using System.Text;
using System.Text.Json;
using Scion.FakeClaude;

Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

// A stand-in for the `claude` executable: `auth status`, `auth login` and `-p` (print mode).
var scriptPath = Environment.GetEnvironmentVariable("SCION_FAKE_CLAUDE_SCRIPT")
    ?? throw new InvalidOperationException("SCION_FAKE_CLAUDE_SCRIPT is not set.");
var script = FakeClaudeScript.Read(scriptPath);

// The grandchild of a test about process trees: it only says who it is and waits to be ended.
if (Environment.GetEnvironmentVariable("SCION_FAKE_CLAUDE_CHILD") == "1")
{
    File.WriteAllText(script.PidPath + ".child", Environment.ProcessId.ToString());
    await Task.Delay(Timeout.Infinite);
}
var signedInMarker = scriptPath + ".signedin";

if (script.PidPath is not null)
{
    File.WriteAllText(script.PidPath, Environment.ProcessId.ToString());
}

if (script.SpawnChild)
{
    await Task.Delay(script.SpawnChildAfterMs);
    SpawnChild();
}

var isPrint = args.Contains("-p");
var standardInput = isPrint ? Console.In.ReadToEnd() : "";

if (script.LogPath is not null)
{
    var entry = new
    {
        args,
        stdin = standardInput,
        cwd = Directory.GetCurrentDirectory(),
        cwdEntries = Directory.GetFileSystemEntries(Directory.GetCurrentDirectory()).Length,
        apiKeySet = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") is not null,
        marker = Environment.GetEnvironmentVariable("SCION_FAKE_CLAUDE_MARKER"),
    };
    File.AppendAllText(script.LogPath, JsonSerializer.Serialize(entry) + "\n");
}

return args switch
{
    ["auth", "status", ..] => AuthStatus(),
    ["auth", "login", ..] => await AuthLoginAsync(),
    _ when isPrint => await PrintAsync(),
    _ => 2,
};

int AuthStatus()
{
    var isSignedIn = script.SignedIn || File.Exists(signedInMarker);
    if (script.AuthStatusNotJson)
    {
        Console.Out.WriteLine("Logged in (not json)");

        return isSignedIn ? 0 : 1;
    }

    // The real one prints more than the kind of sign-in: account and organization too. Scion must take only the kind.
    var fields = new Dictionary<string, object?> { ["loggedIn"] = isSignedIn };
    if (script.AuthMethod is not null)
    {
        fields["authMethod"] = script.AuthMethod;
    }

    if (script.ExtraAuthFields)
    {
        fields["email"] = "someone@example.test";
        fields["orgName"] = "Example Org";
        fields["subscriptionType"] = "team";
    }

    Console.Out.WriteLine(JsonSerializer.Serialize(fields));

    return isSignedIn ? 0 : 1;
}

async Task<int> AuthLoginAsync()
{
    await Task.Delay(script.LoginDelayMs < 0 ? Timeout.Infinite : script.LoginDelayMs);
    File.WriteAllText(signedInMarker, "");

    return 0;
}

async Task<int> PrintAsync()
{
    // The real program streams only with these flags; a caller that leaves one out would get no stream from it either.
    if (!HasFlagValue("--output-format", "stream-json") || !args.Contains("--verbose") || !args.Contains("--include-partial-messages"))
    {
        Console.Error.WriteLine("Error: print mode needs --output-format stream-json with --verbose and --include-partial-messages.");

        return 2;
    }

    var answer = script.Adapt;

    switch (answer.Kind)
    {
        case FakeClaudeScript.AdaptKind.Hang:
            await Task.Delay(Timeout.Infinite);
            return 0;
        case FakeClaudeScript.AdaptKind.NotJson:
            Console.Out.WriteLine("this is not json");
            return 1;
        case FakeClaudeScript.AdaptKind.Success:
            return await StreamSkillFileAsync(answer);
        case FakeClaudeScript.AdaptKind.ResultOnly:
            Write(new { type = "result", subtype = "success", is_error = false, result = answer.SkillFile });
            return 0;
        case FakeClaudeScript.AdaptKind.LimitByStatus:
            Write(new { type = "result", subtype = "success", is_error = true, api_error_status = 429, result = "Request rejected" });
            return 1;
        case FakeClaudeScript.AdaptKind.LimitByText:
            Write(new { type = "result", subtype = "success", is_error = true, result = "You've hit your session limit · resets 5pm" });
            return 1;
        default:
            Write(new { type = "result", subtype = "error", is_error = true, result = answer.Message });
            return 1;
    }
}

// A child that is not a pipe partner of this process (its handles are its own), so it outlives this process unless the
// system ends it with it.
void SpawnChild()
{
    var startInfo = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    startInfo.Environment["SCION_FAKE_CLAUDE_CHILD"] = "1";
    System.Diagnostics.Process.Start(startInfo);
}

bool HasFlagValue(string flag, string value)
{
    var index = Array.IndexOf(args, flag);

    return index >= 0 && index + 1 < args.Length && args[index + 1] == value;
}

// Writes what Claude Code writes for a structured answer: the skill file is the input of the StructuredOutput tool, which
// arrives as input_json_delta pieces (cut anywhere, escapes included), and the result comes last.
async Task<int> StreamSkillFileAsync(FakeClaudeScript.AdaptAnswer answer)
{
    Write(new { type = "system", subtype = "init" });
    WriteEvent(new { type = "message_start" });
    WriteEvent(new { type = "content_block_start", index = 0, content_block = new { type = "tool_use", id = "toolu_fake", name = "StructuredOutput", input = new { } } });

    // The time before the first text: the screen shows its waiting state meanwhile.
    if (answer.FirstChunkDelayMs > 0)
    {
        await Task.Delay(answer.FirstChunkDelayMs);
    }

    var pieces = Cut(JsonSerializer.Serialize(new { skillFile = answer.SkillFile }), answer.ChunkSize);
    for (var index = 0; index < pieces.Count; index++)
    {
        if (answer.StopAfterChunks == index)
        {
            return 1;
        }

        if (answer.HangAfterChunks == index)
        {
            await Task.Delay(Timeout.Infinite);
        }

        WriteEvent(new { type = "content_block_delta", index = 0, delta = new { type = "input_json_delta", partial_json = pieces[index] } });
        if (answer.ChunkDelayMs > 0)
        {
            await Task.Delay(answer.ChunkDelayMs);
        }
    }

    if (answer.StopAfterChunks == pieces.Count)
    {
        return 1;
    }

    if (answer.HangAfterChunks == pieces.Count)
    {
        await Task.Delay(Timeout.Infinite);
    }

    WriteEvent(new { type = "content_block_stop", index = 0 });
    WriteEvent(new { type = "message_stop" });
    Write(new { type = "result", subtype = "success", is_error = false, result = "done", structured_output = new { skillFile = answer.SkillFile } });

    return 0;
}

List<string> Cut(string text, int size)
{
    if (size <= 0)
    {
        return [text];
    }

    return Enumerable.Range(0, (text.Length + size - 1) / size).Select(piece => text.Substring(piece * size, Math.Min(size, text.Length - piece * size))).ToList();
}

void WriteEvent(object streamEvent) => Write(new { type = "stream_event", @event = streamEvent });

void Write(object envelope) => Console.Out.WriteLine(JsonSerializer.Serialize(envelope));
