namespace TLio.Mcp.Configuration;

public class McpConfiguration
{
    public ObservabilityConfig Observability { get; set; } = new();
    public RateLimitConfig RateLimit { get; set; } = new();
    public string AiRefRoot { get; set; } = "";
}

public class ObservabilityConfig
{
    public bool Enabled { get; set; } = true;
}

public class RateLimitConfig
{
    public int RequestsPerMinute { get; set; } = 20;
    public int WindowCount { get; set; } = 6;
}
