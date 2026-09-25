using RecipeBot.Domain.Enums;
using RecipeBot.Domain.Models;

namespace RecipeBot.Application.Models;

public class UserSession
{
    public UserStates UserState { get; set; }

    public RecipeStates RecipeState { get; set; }

    public RecipeDto? RecipeDraft { get; set; }
    public RecipeDto? CurrentViewRecipe { get; set; }
    public StageDto? CurrentViewStage { get; set; }

    public List<RecipeDto>? Recipes  { get; set; }
    
    public long? CurrentUpdateId { get; set; }
    public long? LastUpdateId { get; set; }
    public long ChatId { get; set; }
}