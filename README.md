# Scion

Scion is a desktop app for managing AI agent skills **per project**. Open the app, see your
projects, open one, and see its skills and its agent instruction file (`CLAUDE.md` / `AGENTS.md`).

- View, edit, create and delete skills (a deleted skill goes to Scion's own trash, never gone for good).
- Copy a skill to another project, to your personal skills, or to another agent (Claude Code, Codex).
- **Adapt with AI**: instead of copying a skill as it is, have it adapted to the target project's stack
  and conventions, then review the result side by side before anything is written.

Scion is project-centric: there is no central skill library. Projects are added one by one; Scion keeps
its own metadata in `~/.scion/` and never writes its own files into your repositories.

## Status

Pre-1.0. Developed and tested on Windows. macOS and Linux are targeted but not verified yet
(Linux needs WebKitGTK at runtime).

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (see `global.json`).
- Optional, for *Adapt with AI*: your own installed and signed-in
  [Claude Code](https://www.anthropic.com/claude-code) or Codex CLI. Scion starts the client you choose
  as a subprocess.

## Build and test

```
dotnet build
dotnet test
```

The smoke tests drive the real desktop app and run on Windows only; a plain `dotnet test` skips them:

```
dotnet test tests/Scion.Smoke.Tests --filter Category=Smoke
```

Run the app:

```
dotnet run --project src/Scion.Host.Desktop
```

The `SCION_*` environment variables read by `src/Scion.Host.Desktop/Program.cs` (`SCION_HOME`,
`SCION_TEST_FOLDER`, `SCION_TEST_SHELL_LOG`, `SCION_CLAUDE_PATH`, `SCION_CODEX_PATH`) exist for the
automated tests. The automated tests never call a real Claude or Codex; they use stand-in executables.

## Architecture

Clean Architecture; source-code dependencies point only inward:

```
Domain  <-  Application  <-  Infrastructure / UI  <-  Host
```

| Project | Role |
|---|---|
| `Scion.Domain` | entities, value objects, rules; depends on nothing |
| `Scion.Application` | use cases, ports (interfaces) and DTOs |
| `Scion.Infrastructure.*` | file system, project registry, and the Claude Code and Codex adapters |
| `Scion.UI` | Blazor components in a Razor Class Library; knows only Application |
| `Scion.Host.Desktop` | PhotinoX window and dependency wiring (composition root) |

The Dependency Rule is enforced by `tests/Scion.Architecture.Tests`, which runs with every `dotnet test`.

## What Adapt with AI sends

An adaptation shares only an allowlist of the target: the agent's instruction files, project manifests
(for example `*.csproj`, `package.json`, `go.mod`) and folder names two levels deep, never a source file.
The dialog lists exactly what will be sent before you press *Adapt*, and nothing is written until you
save the reviewed result. Scion never reads, stores or transmits AI credentials; the sign-in belongs to the
client you installed.

## Trademarks

Claude and Anthropic are trademarks of Anthropic, PBC. Codex, ChatGPT and OpenAI are trademarks of
OpenAI. Scion is not affiliated with either company.

## License

MIT, see [LICENSE](LICENSE). Third-party components: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
