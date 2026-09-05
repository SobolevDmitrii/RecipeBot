using Microsoft.EntityFrameworkCore;
using RecipeBot.Domain.Abstract.IRepositories;
using RecipeBot.Domain.Models;

namespace RecipeBot.Infrastructure.Repositories;

public class CategoryRepository(IDbContextFactory<MyRecipeDbContext> contextFactory) : ICategoryRepository
{
    public async Task<List<Category>> GetAllCategories(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Categories.ToListAsync(cancellationToken);
    }
}