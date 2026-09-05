using RecipeBot.Domain.Models;

namespace RecipeBot.Domain.Abstract.IRepositories;

public interface IStageRepository
{
    public Task<Stage> Add (Stage stage, long chatId, CancellationToken cancellationToken);
}