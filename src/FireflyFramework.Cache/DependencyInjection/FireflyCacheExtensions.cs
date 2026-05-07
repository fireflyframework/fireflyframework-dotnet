using FireflyFramework.Cache.Adapters;
using FireflyFramework.Cache.Configuration;
using FireflyFramework.Cache.Core;
using FireflyFramework.Cache.Manager;
using FireflyFramework.Cache.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace FireflyFramework.Cache.DependencyInjection;

public static class FireflyCacheExtensions
{
    public static IServiceCollection AddFireflyCache(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<FireflyCacheOptions>().Bind(config.GetSection(FireflyCacheOptions.SectionName));
        services.TryAddSingleton<ICacheSerializer, JsonCacheSerializer>();
        services.TryAddSingleton<IMemoryCache>(_ => new MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));

        services.AddSingleton<ICacheAdapter>(sp =>
        {
            var opt = (FireflyCacheOptions)sp.GetRequiredService(typeof(Microsoft.Extensions.Options.IOptions<FireflyCacheOptions>))
                .GetType().GetProperty("Value")!.GetValue(sp.GetRequiredService(typeof(Microsoft.Extensions.Options.IOptions<FireflyCacheOptions>)))!;

            return opt.Provider switch
            {
                CacheType.Redis => BuildRedis(sp, opt),
                CacheType.Memory => new MemoryCacheAdapter(sp.GetRequiredService<IMemoryCache>(), opt.Name),
                CacheType.NoOp => new NoopCacheAdapter(),
                _ => new MemoryCacheAdapter(sp.GetRequiredService<IMemoryCache>(), opt.Name),
            };
        });

        return services;
    }

    private static ICacheAdapter BuildRedis(IServiceProvider sp, FireflyCacheOptions opt)
    {
        var mux = ConnectionMultiplexer.Connect(opt.Redis.ConnectionString);
        var ser = sp.GetRequiredService<ICacheSerializer>();
        return new RedisCacheAdapter(mux, ser, opt.Name, opt.KeyPrefix);
    }
}
