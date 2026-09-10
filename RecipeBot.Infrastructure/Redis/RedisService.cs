using System.Text.Json;
using System.Text.Json.Serialization;
using RecipeBot.Application.Abstract;
using RecipeBot.Application.Models;
using StackExchange.Redis;

namespace RecipeBot.Infrastructure.Redis;

public class RedisService : IRedisService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var database = _redis.GetDatabase();

        var value = await database.StringGetAsync(key);

        if (value.IsNullOrEmpty)
            return default;
        
        var json = (string)value;

        return JsonSerializer.Deserialize<T>(json);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null)
    {
        var database = _redis.GetDatabase();

        var json = JsonSerializer.Serialize(value);

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

    public async Task AddProcessingAsync(long userId)
    {
        var database = _redis.GetDatabase();
        
        await database.SortedSetAddAsync(
            "processing:sessions",
            userId.ToString(),
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public async Task<List<long>> GetExpiredProcessingAsync(int minutes)
    {
        var database = _redis.GetDatabase();
        
        var threshold = DateTimeOffset.UtcNow
            .AddMinutes(-minutes)
            .ToUnixTimeSeconds();

         return (await database.SortedSetRangeByScoreAsync(
            "processing:sessions",
            double.NegativeInfinity,
            threshold))
            .Select(q => (long)q)
            .ToList();
    }

    public async Task RemoveProcessingAsync(long userId)
    {
        var database = _redis.GetDatabase();
        await database.SortedSetRemoveAsync(
            "processing:sessions",
            userId.ToString());
    }
    
    public async Task RemoveRangeProcessingAsync(List<long> userIds)
    {
        var database = _redis.GetDatabase();
        var members = userIds
            .Select(id => (RedisValue)id.ToString())
            .ToArray();

        await database.SortedSetRemoveAsync(
            "processing:sessions",
            members);
    }
    
    public async Task<List<UserSession?>> GetUserSessionsAsync(List<long> userIds)
    {
        var database = _redis.GetDatabase();

        var keys = userIds
            .Select(id => (RedisKey)id.ToString())
            .ToArray();

        var values = await database.StringGetAsync(keys);

        return values
            .Select(value =>
                value.HasValue
                    ? JsonSerializer.Deserialize<UserSession>((string)value!)
                    : null)
            .ToList();
    }
}