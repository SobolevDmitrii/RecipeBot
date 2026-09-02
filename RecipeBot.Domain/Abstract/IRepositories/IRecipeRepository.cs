using MyRecipeDomain.Models;

namespace RecipeBot.Domain.Abstract.IRepositories;

public interface IRecipeRepository
{
    public Task<Recipe> Add (Recipe recipe, long chatId, CancellationToken cancellationToken);
}