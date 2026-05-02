using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using TLio.Mcp.Configuration;

namespace TLio.Mcp.Services;

public sealed class RateLimiterService : IDisposable
{
    private readonly SlidingWindowRateLimiter _limiter;

    public RateLimiterService(IOptions<McpConfiguration> config)
    {
        var cfg = config.Value.RateLimit;
        _limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = cfg.RequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = cfg.WindowCount,
            QueueProcessingOrder = QueueProcessingOrder.NewestFirst,
            QueueLimit = 0
        });
    }

    public bool TryAcquire(out int retryAfterSeconds)
    {
        using var lease = _limiter.AttemptAcquire();
        if (lease.IsAcquired)
        {
            retryAfterSeconds = 0;
            return true;
        }

        retryAfterSeconds = lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? (int)Math.Ceiling(retryAfter.TotalSeconds)
            : 3;
        return false;
    }

    public void Dispose() => _limiter.Dispose();
}
