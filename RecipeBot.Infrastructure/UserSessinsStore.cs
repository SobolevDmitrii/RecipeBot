using RecipeBot.Application.Abstract;
using RecipeBot.Application.Models;

namespace RecipeBot.Infrastructure;

public class UserSessionsStore(IRedisService redisService) : IUserSessionStore
{
    public  async Task<UserSession> AddUserSession(long userId)
    {
        var newSession = new UserSession();
        await redisService.SetAsync(userId.ToString(), newSession);
        return newSession;
    }

    public async Task<UserSession> GetOrAddUserSession(long userId)
    {
        var session = await redisService.GetAsync<UserSession>(userId.ToString());
        if (session != null)
        {
            return session;
        }

        return await AddUserSession(userId);
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