using Microsoft.EntityFrameworkCore;
using MyRecipeDomain.Models;
using RecipeBot.Domain.Abstract.IRepositories;

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
            .Where(q=>q.User.TelegramId == chatId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Recipe>> GetListByTelegramIdAndCategory(long chatId, int categoryId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Recipes
            .Include(r => r.User)
            .Where(q=>q.User.TelegramId == chatId && q.CategoryId == categoryId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Recipe>> SearchByName( string text, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Recipes
            .Where(q=>q.Name.ToLower().Contains(text.ToLower()))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Recipe> Get(int id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Recipes
            .Include(q=>q.Stages)
            .Include(q=>q.Category)
            .AsNoTracking()
            .SingleAsync(q => q.Id == id, cancellationToken);
    }

    public async Task Delete(int id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var recipe = await context.Recipes.SingleAsync(q => q.Id == id, cancellationToken);
        context.Recipes.Remove(recipe);
        await context.SaveChangesAsync(cancellationToken);
    }
}