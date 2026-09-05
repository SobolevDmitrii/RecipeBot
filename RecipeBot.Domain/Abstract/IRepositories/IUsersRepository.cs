using RecipeBot.Domain.Models;

namespace RecipeBot.Domain.Abstract.IRepositories;

public interface IUsersRepository
{
    public Task<User> GetByChatId (long chatId,CancellationToken cancellationToken);
    public Task<User> GetOrCreateUserAsync (long chatId, string name,CancellationToken cancellationToken);
}