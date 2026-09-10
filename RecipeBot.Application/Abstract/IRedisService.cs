using RecipeBot.Application.Models;

namespace RecipeBot.Application.Abstract;

public interface IRedisService
{
    Task<T?> GetAsync<T>(string key);

    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);

    Task RemoveAsync(string key);
    
    Task AddProcessingAsync(long userId);

    Task<List<long>> GetExpiredProcessingAsync(int minutes);

    Task RemoveProcessingAsync(long userId);
    Task RemoveRangeProcessingAsync(List<long> userIds);

    Task<List<UserSession?>> GetUserSessionsAsync(List<long> userIds);
}