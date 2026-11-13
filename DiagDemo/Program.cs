using System.Runtime.CompilerServices;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "DiagDemo is running");

// Correct async code: the thread returns to the pool while waiting.
app.MapGet("/healthy", async () =>
{
    await Task.Delay(500);
    return "ok";
});

// BUG 1: sync-over-async. Blocks a thread pool thread for 500 ms.
app.MapGet("/starve", () =>
{
    Task.Delay(500).Wait();
    return "ok";
});

// BUG 2: an inefficient algorithm that burns CPU.
app.MapGet("/cpu", () => $"primes: {Work.CountPrimes(20_000)}");

// BUG 3: a cache that grows forever.
app.MapGet("/leak", () =>
{
    lock (LeakyCache.Items)
    {
        LeakyCache.Items.Add(new CacheEntry());
        return $"cache entries: {LeakyCache.Items.Count}";
    }
});

app.Run();

static class Work
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int CountPrimes(int max)
    {
        var count = 0;
        for (var n = 2; n < max; n++)
            if (IsPrimeSlow(n)) count++;
        return count;
    }

    // Checks every divisor up to n instead of up to sqrt(n).
    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool IsPrimeSlow(int n)
    {
        for (var d = 2; d < n; d++)
            if (n % d == 0) return false;
        return true;
    }
}

static class LeakyCache
{
    public static readonly List<CacheEntry> Items = new();
}

class CacheEntry
{
    public byte[] Payload { get; } = new byte[1024 * 1024]; // 1 MB
    public DateTime Created { get; } = DateTime.UtcNow;
}