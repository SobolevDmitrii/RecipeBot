using System.Collections.Concurrent;
using RecipeBot.Application.Abstract;
using RecipeBot.Application.Models;

namespace RecipeBot.Application;

public class UserSessionsStore(IRedisService redisService) : IUserSessionStore
{
    public async Task<UserSession> GetOrAddUserSession(long userId)
    {
        var session = await redisService.GetAsync<UserSession>(userId.ToString());
        if (session != null)
        {
            return session;
        }
        return new UserSession();
    }

    public async Task<UserSession?> TryGetSession(long userId)
    => await redisService.GetAsync<UserSession>(userId.ToString());
    
    public async Task SetUserSession(long userId, UserSession userSession)
    => await redisService.SetAsync(userId.ToString(), userSession);

    public async Task AddProcessing(long userId)
    => await redisService.AddProcessingAsync(userId);

    public async Task RemoveProcessing(long userId)
    => await redisService.RemoveProcessingAsync(userId);
}