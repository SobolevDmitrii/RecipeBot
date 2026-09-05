using Microsoft.EntityFrameworkCore;
using RecipeBot.Domain.Abstract.IRepositories;
using RecipeBot.Domain.Models;

namespace RecipeBot.Infrastructure.Repositories;

public class UsersRepository(IDbContextFactory<MyRecipeDbContext> contextFactory) : IUsersRepository
{
    public async Task<User> GetByChatId(long chatId,CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Users.FirstAsync(x => x.TelegramId == chatId,  cancellationToken);
    }

    public async Task<User> GetOrCreateUserAsync(long chatId, string name,CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await context.Users.FirstOrDefaultAsync(x => x.TelegramId == chatId,  cancellationToken);
        if (user == null)
        {
            var addedUser =new User();
            addedUser.SetName(name);
            addedUser.SetTelegramId(chatId);
            user = context.Users.Add(addedUser).Entity;
            await context.SaveChangesAsync(cancellationToken);
        }
        return user;
    }
}