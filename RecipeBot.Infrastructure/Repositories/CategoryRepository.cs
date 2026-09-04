using Microsoft.EntityFrameworkCore;
using MyRecipeDomain.Models;
using RecipeBot.Domain.Abstract.IRepositories;

namespace RecipeBot.Infrastructure.Repositories;

public class CategoryRepository(IDbContextFactory<MyRecipeDbContext> contextFactory) : ICategoryRepository
{
    public async Task<List<Category>> GetAllCategories(CancellationToken cancellationToken)
    {
        var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Categories.ToListAsync(cancellationToken);
    }
}