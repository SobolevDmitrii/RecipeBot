namespace RecipeBot.Domain.Enums;

public enum RecipeStates
{
    WaitingForCategory,
    WaitingForRecipeName,
    WaitingForIngredients,
    AddingStage,
    WaitingMove,
    WaitingForImageRecipe,
    ShowRecipeSelectionOptions,
    ShowRecipes,
    WaitMoveForRecipe,
    GetMenuEdit,
    WaitingForSelectStage,
    WaitingForStage,
    WaitingMoveForEditStage,
    WaitingImageStage,
    WaitingDescription
}