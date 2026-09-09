using System.Text.Json;
using System.Text.Json.Serialization;
using RecipeBot.Application.Abstract;
using StackExchange.Redis;

namespace RecipeBot.Infrastructure.Redis;

public class RedisService : IRedisService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public async Task<T?> GetAsync<T>(string key)
    {
        var database = _redis.GetDatabase();

        var value = await database.StringGetAsync(key);

        if (value.IsNullOrEmpty)
            return default;
        
        var json = (string)value;

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null)
    {
        var database = _redis.GetDatabase();

        var json = JsonSerializer.Serialize(value, JsonOptions);

        await database.StringSetAsync(
            key,
            json,
            expiration.HasValue
                ? new Expiration(expiration.Value)
                : Expiration.Default);
    }

    public async Task RemoveAsync(string key)
    {
        var database = _redis.GetDatabase();

        await database.KeyDeleteAsync(key);
    }
}