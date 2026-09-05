using RecipeBot.Domain.Models;

namespace RecipeBot.Domain.Abstract.IRepositories;

public interface ICategoryRepository
{
    public Task<List<Category>> GetAllCategories (CancellationToken cancellationToken);
}