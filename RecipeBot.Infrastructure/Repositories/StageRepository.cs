using Microsoft.EntityFrameworkCore;
using RecipeBot.Domain.Abstract.IRepositories;
using RecipeBot.Domain.Models;

namespace RecipeBot.Infrastructure.Repositories;

public class StageRepository(IDbContextFactory<MyRecipeDbContext> contextFactory) : IStageRepository
{
    public async Task<Stage> Add (Stage createStage, long chatId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var stage = context.Stages.Add(createStage).Entity;
        await context.SaveChangesAsync(cancellationToken);
        return stage;
    }
}