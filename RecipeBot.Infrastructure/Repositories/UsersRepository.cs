using Microsoft.EntityFrameworkCore;
using MyRecipeDomain.Models;
using RecipeBot.Domain.Abstract.IRepositories;

namespace RecipeBot.Infrastructure.Repositories;

public class UsersRepository(IDbContextFactory<MyRecipeDbContext> contextFactory) : IUsersRepository
{
    public async Task<User> GetByChatId(long chatId,CancellationToken cancellationToken)
    {
        var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Users.FirstAsync(x => x.TelegramId == chatId,  cancellationToken);
    }
}