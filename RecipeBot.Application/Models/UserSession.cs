using MyRecipeDomain.Models;
using RecipeBot.Domain.Enums;

namespace RecipeBot.Application.Models;

public class UserSession
{
    public States UserState { get; set; }

    public States RecipeState { get; set; }

    public Recipe? RecipeDraft { get; set; }
}