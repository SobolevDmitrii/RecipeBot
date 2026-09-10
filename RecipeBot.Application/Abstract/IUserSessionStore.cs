using RecipeBot.Application.Models;

namespace RecipeBot.Application.Abstract;

public interface IUserSessionStore
{
    public Task<UserSession> GetOrAddUserSession(long userId);
    public Task<UserSession?> TryGetSession(long userId);
    public Task SetUserSession(long userId, UserSession userSession);
    public Task AddProcessing(long userId);
    public Task RemoveProcessing(long userId);
}