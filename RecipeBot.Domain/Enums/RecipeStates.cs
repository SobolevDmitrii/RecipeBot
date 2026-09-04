namespace RecipeBot.Domain.Enums;

public enum RecipeStates
{
    WaitingForCategory,
    WaitingForRecipeName,
    WaitingForIngredients,
    AddingStage,
    WaitingMove,
    WaitingForImageRecipe
}