using RecipeBot.Application.Models;
using RecipeBot.Domain.Models;

namespace RecipeBot.Application.Mappers.Extensions;

public static class RecipeExtension
{
    public static RecipeDto ToDto(this Recipe recipe)
        => new RecipeDto()
        {
            Id =  recipe.Id,
            CategoryId = recipe.CategoryId,
            Ingredients = recipe.Ingredients,
            Name = recipe.Name,
            Stages = recipe.Stages.ToDto(),
            UrlImage = recipe.UrlImage
        };
    
    public static List<RecipeDto> ToDto(this List<Recipe> recipes)
        => [..recipes.Select(ToDto)];

    public static Recipe ToEntity(this RecipeDto recipe)
    {
        var recipeEntity = new Recipe();
        recipeEntity.SetCategoryId(recipe.CategoryId);
        recipeEntity.SetName(recipe.Name);
        recipeEntity.SetIngredients(recipe.Ingredients);
        if (recipe.UrlImage != null)
        {
            recipeEntity.SetUrlImage(recipe.UrlImage);
        }
        recipeEntity.AddRangeStage(recipe.Stages.ToEntity());
        return recipeEntity;
    }
    
    public static List<Recipe> ToEntity(this List<RecipeDto> stages)
        => [..stages.Select(ToEntity)];
}