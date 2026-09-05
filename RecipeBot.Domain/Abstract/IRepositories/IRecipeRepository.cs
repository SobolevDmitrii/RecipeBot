using MyRecipeDomain.Models;

namespace RecipeBot.Domain.Abstract.IRepositories;

public interface IRecipeRepository
{
    public Task<Recipe> Add (Recipe recipe, long chatId, CancellationToken cancellationToken);
    public Task<List<Recipe>> GetListByTelegramId (long chatId, CancellationToken cancellationToken);
    public Task<List<Recipe>> GetListByTelegramIdAndCategory (long chatId,int categoryId, CancellationToken cancellationToken);
    public Task<List<Recipe>> SearchByName (string text, CancellationToken cancellationToken);
    public Task<Recipe> Get(int id, CancellationToken cancellationToken);
    public Task Delete(int id, CancellationToken cancellationToken);
}