using Microsoft.Extensions.Options;
using NUnit.Framework;
using TLio.Mcp.Configuration;
using TLio.Mcp.Services;

namespace TLio.Mcp.Tests;

[TestFixture]
public sealed class RateLimiterTests
{
    private static RateLimiterService CreateLimiter(int requestsPerMinute, int windowCount = 1)
    {
        var config = Options.Create(new McpConfiguration
        {
            RateLimit = new RateLimitConfig
            {
                RequestsPerMinute = requestsPerMinute,
                WindowCount = windowCount
            }
        });
        return new RateLimiterService(config);
    }

    [Test]
    public void TryAcquire_WithinLimit_Succeeds()
    {
        using var sut = CreateLimiter(requestsPerMinute: 5);

        var acquired = sut.TryAcquire(out var retryAfter);

        Assert.That(acquired, Is.True);
        Assert.That(retryAfter, Is.EqualTo(0));
    }

    [Test]
    public void TryAcquire_ExceedsLimit_Fails()
    {
        using var sut = CreateLimiter(requestsPerMinute: 2);

        // Exhaust the permit budget
        sut.TryAcquire(out _);
        sut.TryAcquire(out _);

        var acquired = sut.TryAcquire(out var retryAfter);

        Assert.That(acquired, Is.False);
        Assert.That(retryAfter, Is.GreaterThan(0));
    }

    [Test]
    public void TryAcquire_ExactlyAtLimit_AllSucceed()
    {
        const int limit = 3;
        using var sut = CreateLimiter(requestsPerMinute: limit);

        var results = Enumerable.Range(0, limit)
            .Select(_ => sut.TryAcquire(out _))
            .ToList();

        Assert.That(results, Has.All.True);
    }

    [Test]
    public void TryAcquire_AfterExceeding_RetryAfterIsPositive()
    {
        using var sut = CreateLimiter(requestsPerMinute: 1);
        sut.TryAcquire(out _); // consume the only permit

        sut.TryAcquire(out var retryAfter);

        Assert.That(retryAfter, Is.GreaterThanOrEqualTo(1));
    }
}
