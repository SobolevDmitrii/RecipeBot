using Microsoft.EntityFrameworkCore;
using MyRecipeDomain.Models;
using RecipeBot.Domain.Abstract.IRepositories;

namespace RecipeBot.Infrastructure.Repositories;

public class RecipeRepository(IDbContextFactory<MyRecipeDbContext> contextFactory,IUsersRepository usersRepository): IRecipeRepository
{
    public async Task<Recipe> Add(Recipe createRecipe, long chatId, CancellationToken cancellationToken)
    {
        var user = await usersRepository.GetByChatId(chatId, cancellationToken);
        var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        createRecipe.UserId = user.Id;
        var recipe = context.Recipes.Add(createRecipe).Entity;
        await context.SaveChangesAsync(cancellationToken);
        return recipe;
    }
}