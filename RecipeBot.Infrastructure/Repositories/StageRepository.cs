using Microsoft.EntityFrameworkCore;
using MyRecipeDomain.Models;
using RecipeBot.Domain.Abstract.IRepositories;

namespace RecipeBot.Infrastructure.Repositories;

public class StageRepository(IDbContextFactory<MyRecipeDbContext> contextFactory,IUsersRepository usersRepository) : IStageRepository
{
    public async Task<Stage> Add (Stage createStage, long chatId, CancellationToken cancellationToken)
    {
        var user = await usersRepository.GetByChatId(chatId, cancellationToken);
        var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var stage = context.Stages.Add(createStage).Entity;
        await context.SaveChangesAsync(cancellationToken);
        return stage;
    }
}