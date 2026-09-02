using MyRecipeDomain.Models;

namespace RecipeBot.Domain.Abstract.IRepositories;

public interface IUsersRepository
{
    public Task<User> GetByChatId (long chatId,CancellationToken cancellationToken);
}