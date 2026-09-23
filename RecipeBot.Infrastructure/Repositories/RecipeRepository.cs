using Microsoft.EntityFrameworkCore;
using RecipeBot.Domain.Abstract.IRepositories;
using RecipeBot.Domain.Models;

namespace RecipeBot.Infrastructure.Repositories;

public class RecipeRepository(IDbContextFactory<MyRecipeDbContext> contextFactory, IUsersRepository usersRepository)
    : IRecipeRepository
{
    public async Task<Recipe> Add(Recipe createRecipe, long chatId, CancellationToken cancellationToken)
    {
        var user = await usersRepository.GetByChatId(chatId, cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        createRecipe.SetUserId(user.Id);
        var recipe = context.Recipes.Add(createRecipe).Entity;
        await context.SaveChangesAsync(cancellationToken);
        return recipe;
    }

    public async Task<List<Recipe>> GetListByTelegramId(long chatId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Recipes
            .Include(r => r.User)
            .Where(q => q.User.TelegramId == chatId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Recipe>> GetListByTelegramIdAndCategory(long chatId, int categoryId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Recipes
            .Include(r => r.User)
            .Where(q => q.User.TelegramId == chatId && q.CategoryId == categoryId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Recipe>> SearchByName(long chatId, string text, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Recipes
            .Include(r => r.User)
            .Where(q => q.Name.ToLower().Contains(text.ToLower())
                        && q.User.TelegramId == chatId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Recipe> Get(long chatId, int id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Recipes
            .Include(q => q.Stages.OrderBy(stage => stage.Number))
            .Include(q => q.Category)
            .Include(r => r.User)
            .AsNoTracking()
            .SingleAsync(q => q.Id == id && q.User.TelegramId == chatId, cancellationToken);
    }

    public async Task Delete(long chatId, int id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var recipe = await context.Recipes
            .Include(r => r.User)
            .SingleAsync(q => q.Id == id && q.User.TelegramId == chatId, cancellationToken);
        context.Recipes.Remove(recipe);
        await context.SaveChangesAsync(cancellationToken);
    }
}