using System.Collections.Concurrent;
using RecipeBot.Application.Abstract;
using RecipeBot.Application.Models;

namespace RecipeBot.Application;

public class UserSessionsStore(IRedisService redisService) : IUserSessionStore
{
    /*public static ConcurrentDictionary<long, States> UserState = new();
    public static ConcurrentDictionary<long, States> RecipeState = new();
    public static ConcurrentDictionary<long, Recipe> RecipeDrafts  = new();*/
    
    public static readonly ConcurrentDictionary<long, UserSession> UserSessions = new();

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
    {
        var session = await redisService.GetAsync<UserSession>(userId.ToString());

        Console.WriteLine(
            $"GET {userId}: " +
            $"State={session?.UserState}, " +
            $"RecipeState={session?.RecipeState}, " +
            $"Recipe={session?.RecipeDraft?.Name}, " +
            $"Ingredients={session?.RecipeDraft?.Ingredients}, " +
            $"Stages={session?.RecipeDraft?.Stages?.Count}");

        return session;
    }

    public async Task SetUserSession(long userId, UserSession userSession)
    {
        Console.WriteLine(
            $"SET {userId}: " +
            $"State={userSession.UserState}, " +
            $"RecipeState={userSession.RecipeState}, " +
            $"Recipe={userSession.RecipeDraft?.Name}, " +
            $"Ingredients={userSession.RecipeDraft?.Ingredients}, " +
            $"Stages={userSession.RecipeDraft?.Stages?.Count}");
        await redisService.SetAsync(userId.ToString(), userSession);
    }
    
}