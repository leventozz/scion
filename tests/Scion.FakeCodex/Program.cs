using System.Text;
using System.Text.Json;
using Scion.FakeCodex;

Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

// A stand-in for the `codex` executable: `login status`, `login` and `exec`.
var scriptPath = Environment.GetEnvironmentVariable("SCION_FAKE_CODEX_SCRIPT")
    ?? throw new InvalidOperationException("SCION_FAKE_CODEX_SCRIPT is not set.");
var script = FakeCodexScript.Read(scriptPath);

// The grandchild of a test about process trees: it only says who it is and waits to be ended.
if (Environment.GetEnvironmentVariable("SCION_FAKE_CODEX_CHILD") == "1")
{
    File.WriteAllText(script.PidPath + ".child", Environment.ProcessId.ToString());
    await Task.Delay(Timeout.Infinite);
}

var signedInMarker = scriptPath + ".signedin";

// What every real run is started with (the flags Scion needs to keep the client from using tools and from reading
// anything of the user's); a caller that leaves one out is refused, as a test failure and not as a quiet difference.
string[] disabledFeatures =
[
    "shell_tool", "unified_exec", "view_image", "apps", "plugins", "memories", "multi_agent", "browser_use", "computer_use",
    "image_generation", "hooks", "skill_search", "code_mode_host", "sleep_tool", "tool_suggest",
];

if (script.PidPath is not null)
{
    File.WriteAllText(script.PidPath, Environment.ProcessId.ToString());
}

if (script.SpawnChild)
{
    await Task.Delay(script.SpawnChildAfterMs);
    SpawnChild();
}

var isExec = args.Length > 0 && args[0] == "exec";
var standardInput = isExec ? Console.In.ReadToEnd() : "";

if (script.LogPath is not null)
{
    var schemaPath = ValueOf("--output-schema");
    var entry = new
    {
        args,
        stdin = standardInput,
        cwd = Directory.GetCurrentDirectory(),
        cwdEntries = Directory.GetFileSystemEntries(Directory.GetCurrentDirectory()).Length,
        workDirectory = ValueOf("-C"),
        schemaPath,
        schema = schemaPath is not null && File.Exists(schemaPath) ? File.ReadAllText(schemaPath) : null,
        apiKeySet = Environment.GetEnvironmentVariable("CODEX_API_KEY") is not null,
    };
    File.AppendAllText(script.LogPath, JsonSerializer.Serialize(entry) + "\n");
}

return args switch
{
    ["login", "status", ..] => LoginStatus(),
    ["login", ..] => await LoginAsync(),
    ["exec", ..] => await ExecAsync(),
    _ => 2,
};

int LoginStatus()
{
    var isSignedIn = script.SignedIn || File.Exists(signedInMarker);
    Console.Out.WriteLine(isSignedIn ? "Logged in using ChatGPT" : "Not logged in");

    return isSignedIn ? 0 : 1;
}

async Task<int> LoginAsync()
{
    await Task.Delay(script.LoginDelayMs < 0 ? Timeout.Infinite : script.LoginDelayMs);
    File.WriteAllText(signedInMarker, "");

    return 0;
}

async Task<int> ExecAsync()
{
    if (MissingFlag() is { } missing)
    {
        Console.Error.WriteLine($"Error: exec was not started with {missing}.");

        return 2;
    }

    var answer = script.Exec;
    if (answer.DelayMs > 0 && answer.Kind != FakeCodexScript.ExecKind.Hang)
    {
        await Task.Delay(answer.DelayMs);
    }

    switch (answer.Kind)
    {
        case FakeCodexScript.ExecKind.Hang:
            WriteStart();
            await Task.Delay(Timeout.Infinite);
            return 0;
        case FakeCodexScript.ExecKind.ToolRan:
            WriteStart();
            Write(new { type = "item.completed", item = new { id = "item_1", type = "command_execution", command = "echo hi", status = "completed", exit_code = 0 } });
            await Task.Delay(Timeout.Infinite);
            return 0;
        case FakeCodexScript.ExecKind.ToolBlocked:
            WriteStart();
            Write(new { type = "item.completed", item = new { id = "item_1", type = "error", message = "The shell tool is not available." } });
            return WriteAnswer(answer.SkillFile);
        case FakeCodexScript.ExecKind.TurnFailed:
            WriteStart();
            Write(new { type = "error", message = "Reconnecting... 2/5 (" + answer.Message + ")" });
            Write(new { type = "turn.failed", error = new { message = answer.Message } });
            return 1;
        case FakeCodexScript.ExecKind.UsageLimit:
            WriteStart();
            Write(new { type = "turn.failed", error = new { message = "You've hit your usage limit. Upgrade to Pro, or try again later." } });
            return 1;
        case FakeCodexScript.ExecKind.NotJson:
            WriteStart();
            Write(new { type = "item.completed", item = new { id = "item_1", type = "agent_message", text = answer.Message } });
            Write(new { type = "turn.completed", usage = new { input_tokens = 1 } });
            return 0;
        case FakeCodexScript.ExecKind.CrashWithoutAnswer:
            Console.Error.WriteLine(answer.Message);
            return 1;
        default:
            WriteStart();
            return WriteAnswer(answer.SkillFile);
    }
}

// The events every real run starts with: the code-mode warning is there in each one.
void WriteStart()
{
    Write(new { type = "thread.started", thread_id = "01a1a70f-396e-7942-8c71-6caf76da5928" });
    Write(new
    {
        type = "item.completed",
        item = new { id = "item_0", type = "error", message = "Code Mode is unavailable because code-mode host is disabled. Code mode will fail closed." },
    });
    Write(new { type = "turn.started" });
}

int WriteAnswer(string skillFile)
{
    Write(new { type = "item.completed", item = new { id = "item_9", type = "agent_message", text = JsonSerializer.Serialize(new { skillFile }) } });
    Write(new { type = "turn.completed", usage = new { input_tokens = 1 } });

    return 0;
}

string? MissingFlag()
{
    string[] required = ["-", "--json", "--ephemeral", "--ignore-user-config", "--ignore-rules", "--skip-git-repo-check"];
    if (required.FirstOrDefault(flag => !args.Contains(flag)) is { } absent)
    {
        return absent;
    }

    if (ValueOf("--sandbox") != "read-only")
    {
        return "--sandbox read-only";
    }

    if (ValueOf("-C") is not { Length: > 0 })
    {
        return "-C <folder>";
    }

    if (ValueOf("--output-schema") is not { Length: > 0 } schema || !File.Exists(schema))
    {
        return "--output-schema <existing file>";
    }

    var disabled = ValuesOf("--disable").ToHashSet();
    if (disabledFeatures.FirstOrDefault(feature => !disabled.Contains(feature)) is { } feature)
    {
        return $"--disable {feature}";
    }

    return ValuesOf("-c").Contains("web_search=\"disabled\"") ? null : "-c web_search=\"disabled\"";
}

string? ValueOf(string flag) => ValuesOf(flag).FirstOrDefault();

IEnumerable<string> ValuesOf(string flag) =>
    args.Select((argument, index) => (argument, index))
        .Where(pair => pair.argument == flag && pair.index + 1 < args.Length)
        .Select(pair => args[pair.index + 1]);

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
    startInfo.Environment["SCION_FAKE_CODEX_CHILD"] = "1";
    System.Diagnostics.Process.Start(startInfo);
}

void Write(object envelope) => Console.Out.WriteLine(JsonSerializer.Serialize(envelope));
