using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using TLio.Mcp.Configuration;
using TLio.Mcp.Services;
using TLio.Mcp.Tools;

var builder = Host.CreateApplicationBuilder(args);

// ── Environment variable overrides ───────────────────────────────────────────
// TLIO_MCP_TRACE=false  → Observability:Enabled = false
// TLIO_MCP_RATELIMIT=N  → RateLimit:RequestsPerMinute = N
// TLIO_MCP_AIREF=<path> → AiRefRoot = <path>
var overrides = new Dictionary<string, string?>();
if (Environment.GetEnvironmentVariable("TLIO_MCP_TRACE") is { } traceEnv)
    overrides["Observability:Enabled"] = traceEnv.Trim().ToLowerInvariant() != "false" ? "true" : "false";
if (Environment.GetEnvironmentVariable("TLIO_MCP_RATELIMIT") is { } limitEnv)
    overrides["RateLimit:RequestsPerMinute"] = limitEnv.Trim();
if (Environment.GetEnvironmentVariable("TLIO_MCP_AIREF") is { } aiRefEnv)
    overrides["AiRefRoot"] = aiRefEnv.Trim();
builder.Configuration.AddInMemoryCollection(overrides);

// ── Logging — all to stderr so stdout stays clean for MCP ───────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole(opts => opts.LogToStandardErrorThreshold = LogLevel.Trace);

// ── Configuration ────────────────────────────────────────────────────────────
builder.Services.Configure<McpConfiguration>(builder.Configuration);

// ── Application services ─────────────────────────────────────────────────────
builder.Services.AddSingleton<RateLimiterService>();
builder.Services.AddSingleton<AiRefReader>();
builder.Services.AddSingleton<DocumentService>();
builder.Services.AddSingleton<StructuralDiffService>();

// ── MCP server ───────────────────────────────────────────────────────────────
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<DiscoveryTools>()
    .WithTools<ExecutionTools>()
    .WithTools<AnalysisTools>();

await builder.Build().RunAsync();
