using Microsoft.Extensions.Caching.Memory;

namespace TitleFlow.Api.Application.Services;

public sealed class PublicationTitleCache(IMemoryCache cache)
{
    private long _version;

    public Task<T> GetOrCreateAsync<T>(string key, TimeSpan lifetime, Func<Task<T>> factory)
    {
        var version = Interlocked.Read(ref _version);
        return cache.GetOrCreateAsync($"publication-title:{version}:{key}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = lifetime;
            return factory();
        })!;
    }

    public void Invalidate() => Interlocked.Increment(ref _version);
}
