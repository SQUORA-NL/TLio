# Quickstart: TLio MCP Server

## Build

```sh
dotnet build TLio.Mcp/TLio.Mcp.csproj
```

## Configure

Edit `TLio.Mcp/appsettings.json` (or set environment variables):

```json
{
  "Observability": { "Enabled": true },
  "RateLimit": { "RequestsPerMinute": 20, "WindowCount": 6 },
  "AiRefRoot": ""
}
```

| Setting | Env var override | Description |
|---------|-----------------|-------------|
| `Observability:Enabled` | `TLIO_MCP_TRACE=false` | Set to `false` in high-performance environments |
| `RateLimit:RequestsPerMinute` | `TLIO_MCP_RATELIMIT=50` | Max requests per minute per client |
| `AiRefRoot` | `TLIO_MCP_AIREF=<path>` | Absolute path to `docs/ai-ref/`; auto-detected if empty |

## Run (stdio mode)

```sh
dotnet run --project TLio.Mcp/TLio.Mcp.csproj
```

The process reads MCP JSON-RPC messages from stdin and writes responses to stdout. Do not write anything else to stdout from the host process.

## Add to Claude Code

Add to `.claude/mcp_servers.json` in your project root (or `~/.claude/mcp_servers.json` for global):

```json
{
  "mcpServers": {
    "tlio": {
      "command": "dotnet",
      "args": ["run", "--project", "<absolute-path>/TLio.Mcp/TLio.Mcp.csproj", "--no-build"],
      "env": {
        "TLIO_MCP_TRACE": "true"
      }
    }
  }
}
```

## Add to Claude Desktop

In `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "tlio": {
      "command": "dotnet",
      "args": ["<absolute-path>/TLio.Mcp/bin/Release/net10.0/TLio.Mcp.exe"],
      "env": {
        "TLIO_MCP_TRACE": "true",
        "TLIO_MCP_RATELIMIT": "20"
      }
    }
  }
}
```

## Available Tools

| Tool | What it does |
|------|-------------|
| `tlio_list_commands` | Lists all TLio commands with intent summaries |
| `tlio_list_functions` | Lists all TLio functions with intent summaries |
| `tlio_describe` | Returns full documentation for a named command, function, or adapter |
| `tlio_execute` | Runs a TLio script against a document; returns output + execution trace |
| `tlio_analyze` | Computes the structural gap between input and target; returns change items |

## Typical Agent Workflow (Script Authoring)

```
1. tlio_list_commands          → pick relevant commands
2. tlio_describe name=Set      → understand syntax
3. tlio_execute (first attempt) → see trace; identify no-ops and failures
4. tlio_analyze                → get gap report if result doesn't match target
5. tlio_execute (refined)      → verify convergence (≤ 3 iterations expected)
```

## Disable Observability (High-Performance)

```sh
TLIO_MCP_TRACE=false dotnet run --project TLio.Mcp/TLio.Mcp.csproj
```

Execution trace is omitted from all responses. No instrumentation overhead.
