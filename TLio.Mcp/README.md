# TLio MCP server

A [Model Context Protocol](https://modelcontextprotocol.io) server that lets an AI agent run TLio
scripts, check its work against a target document, and look up the command and function
reference — so it writes scripts against what TLio actually supports instead of guessing.

It speaks MCP over **stdio**: your MCP client starts it as a child process. There is nothing to
host, no port, no API key.

## What it gives the agent

| Tool | Use |
|---|---|
| `tlio_guide` | Decision trees (which command / function for which goal) and the critical rules. **Call first.** |
| `tlio_list_commands` | Every command with a one-line intent |
| `tlio_list_functions` | Every function with a one-line intent |
| `tlio_describe` | Full page for one command, function or adapter: syntax, examples, *when not to use*, common mistakes |
| `tlio_execute` | Run a script against a document; returns the output, a per-command trace (`success` / `noop` / `failure`) and suggestions for every noop and failure |
| `tlio_analyze` | Structural diff between an input and a target document — the gap a script has to close |

Everything is registered on the engine: the built-in commands and functions, the ETL, Looping,
Math, Text and TimeDate packs (so `forEach` / `while` work), and `convert` / `convertValue`.
Documents and scripts may be JSON, XML or YAML.

## 1. Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (the server targets `net10.0`)
- This repository cloned locally

## 2. Build it

Publish once to a stable folder. Publishing also copies `docs/ai-ref` next to the binary, so the
server finds its reference docs wherever you put it.

```sh
dotnet publish TLio.Mcp -c Release -f net10.0 -o ~/tools/tlio-mcp
```

`-f net10.0` is required — the project lists its target framework in the plural form, and
`dotnet publish` refuses to guess.

Check that it starts (it waits for a client on stdin; Ctrl+C to stop):

```sh
dotnet ~/tools/tlio-mcp/TLio.Mcp.dll
```

Re-run the publish after pulling changes — the client keeps using whatever is in that folder.

## 3. Register it with your client

### Claude Code

```sh
claude mcp add tlio -- dotnet ~/tools/tlio-mcp/TLio.Mcp.dll
```

Add `--scope user` to make it available in every project, or `--scope project` to write a
`.mcp.json` you can commit for your team. Verify with `claude mcp list`, or `/mcp` inside a
session — `tlio` should show as connected with six tools.

### Claude Desktop

Edit `claude_desktop_config.json` (Settings → Developer → Edit Config) and restart the app:

```json
{
  "mcpServers": {
    "tlio": {
      "command": "dotnet",
      "args": ["/Users/you/tools/tlio-mcp/TLio.Mcp.dll"]
    }
  }
}
```

Use an absolute path; `~` is not expanded here. On Windows use
`"C:\\Users\\you\\tools\\tlio-mcp\\TLio.Mcp.dll"`.

### VS Code / Cursor / other clients

Any client that launches stdio servers takes the same two pieces: command `dotnet`, argument
the path to `TLio.Mcp.dll`. In VS Code that is `.vscode/mcp.json`:

```json
{
  "servers": {
    "tlio": { "type": "stdio", "command": "dotnet", "args": ["/Users/you/tools/tlio-mcp/TLio.Mcp.dll"] }
  }
}
```

### Without publishing (working on TLio itself)

Run straight from source. Slower to start, and always the code you have checked out:

```sh
claude mcp add tlio -- dotnet run --project /path/to/TLio/TLio.Mcp -c Release -f net10.0
```

## 4. Try it

Ask the agent something like:

> Use the tlio tools. Read `tlio_guide`, then write a script that doubles every `price` in
> `{"items":[{"price":1},{"price":2}]}` and run it.

A healthy session calls `tlio_guide`, then `tlio_describe` for the commands it picked, then
`tlio_execute`, and fixes the script from the trace if any command reports `noop`.

## Configuration

All optional. Defaults live in `appsettings.json` next to the binary; environment variables
override them.

| Environment variable | Setting | Default | Meaning |
|---|---|---|---|
| `TLIO_MCP_RATELIMIT` | `RateLimit:RequestsPerMinute` | `20` | Calls per minute across all tools; excess calls get `rate_limit_exceeded` with a retry time |
| `TLIO_MCP_TRACE` | `Observability:Enabled` | `true` | `false` drops the per-command trace from `tlio_execute` results |
| `TLIO_MCP_AIREF` | `AiRefRoot` | *(bundled `docs/ai-ref`)* | Read the reference docs from another folder, e.g. a checkout you are editing |

Pass them through the client:

```sh
claude mcp add tlio --env TLIO_MCP_RATELIMIT=60 -- dotnet ~/tools/tlio-mcp/TLio.Mcp.dll
```

```json
{ "mcpServers": { "tlio": { "command": "dotnet", "args": ["…/TLio.Mcp.dll"], "env": { "TLIO_MCP_RATELIMIT": "60" } } } }
```

Logs go to **stderr**; stdout is reserved for the protocol, so nothing you add should write to it.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Client shows the server as failed / not connected | Run `dotnet <path>/TLio.Mcp.dll` by hand. `command not found: dotnet` → the client's `PATH` lacks it; put the full path to `dotnet` in `command` |
| `You must install or update .NET` | Only an older runtime is installed; the server needs .NET 10 |
| `tlio_describe` says `not_found` or `tlio_guide` says "Guide not found" | The reference docs were not found. Re-publish (it bundles them) or set `TLIO_MCP_AIREF` to a `docs/ai-ref` folder |
| `rate_limit_exceeded` | Raise `TLIO_MCP_RATELIMIT` |
| A command works in `tlio_describe` but fails with "unknown command" | The server is a stale publish — re-run step 2 |
| Changes to the server do not show up | The client caches the running process: restart the client (or `/mcp` → reconnect in Claude Code) |

## Related

- Command and function reference served by the tools: [`docs/ai-ref`](../docs/ai-ref/)
- Notation (JSON / XML / YAML scripts): [`docs/ai-ref/notation-reference.md`](../docs/ai-ref/notation-reference.md)
- Looping commands: [`forEach`](../docs/ai-ref/commands/ForEach.md), [`while`](../docs/ai-ref/commands/While.md)
