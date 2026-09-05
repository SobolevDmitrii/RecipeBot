using RecipeBot.Domain.Models;

namespace RecipeBot.Domain.Abstract.IRepositories;

public interface IRecipeRepository
{
    public Task<Recipe> Add (Recipe recipe, long chatId, CancellationToken cancellationToken);
    public Task<List<Recipe>> GetListByTelegramId (long chatId, CancellationToken cancellationToken);
    public Task<List<Recipe>> GetListByTelegramIdAndCategory (long chatId,int categoryId, CancellationToken cancellationToken);
    public Task<List<Recipe>> SearchByName (long chatId,string text, CancellationToken cancellationToken);
    public Task<Recipe> Get(long chatId,int id, CancellationToken cancellationToken);
    public Task Delete(long chatId,int id, CancellationToken cancellationToken);
}