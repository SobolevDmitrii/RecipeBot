using System.Text.Json.Serialization;

namespace RecipeBot.Domain.Models;

public class Stage
{
    public int Id { get; private set; }
    public string Description { get; private set; }
    public int Number { get;private set; }
    public string? UrlImage { get; private set; }
    public int RecipeId { get; private set; }
    public virtual Recipe Recipe { get;private set; }
    
    public void SetDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty");
        Description = description;
    }

    public void SetNumber(int number)
    {
        if (number < 0)
            throw new ArgumentException("Number cannot be negative");
        Number = number;
    }
    public void SetUrlImage(string urlImage)
    {
        if (string.IsNullOrWhiteSpace(urlImage))
            throw new ArgumentException("UrlImage cannot be empty");
        UrlImage = urlImage;
    }

    public void SetRecipe(int recipeId)
    {
        if (recipeId < 1)
            throw new ArgumentException("RecipeId cannot be negative");
        RecipeId = recipeId;
    }
    
}