using RecipeBot.Domain.Enums;
using RecipeBot.Domain.Models;

namespace RecipeBot.Application.Models;

public class UserSession
{
    public UserStates UserState { get; set; }

    public RecipeStates RecipeState { get; set; }

    public Recipe? RecipeDraft { get; set; }
    public Recipe? CurrentViewRecipe { get; set; }

    public List<Recipe>? Recipes  { get; set; }
}