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

    public async Task<User> GetOrCreateUserAsync(long chatId, string name,CancellationToken cancellationToken)
    {
        var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await context.Users.FirstOrDefaultAsync(x => x.TelegramId == chatId,  cancellationToken);
        if (user == null)
        {
            user = context.Users.Add(new User
            {
                TelegramId = chatId,
                Name = name
            }).Entity;
            await context.SaveChangesAsync(cancellationToken);
        }
        return user;
    }
}