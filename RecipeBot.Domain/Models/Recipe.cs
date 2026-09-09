using System.Text.Json.Serialization;

namespace RecipeBot.Domain.Models;

public class Recipe
{
    [JsonInclude]
    public int Id { get; private set; }
    [JsonInclude]
    public string Name { get; private set; }
    [JsonInclude]
    public string? UrlImage { get; private set; }
    [JsonInclude]
    public string Ingredients { get; private set; }
    [JsonInclude]
    public virtual List<Stage> Stages { get; private set; } = [];
    [JsonInclude]
    public virtual User User { get; private set; }
    [JsonInclude]
    public virtual Category Category { get; private set; }
    [JsonInclude]
    public int CategoryId { get; private set; }
    [JsonInclude]
    public int UserId { get; private set; }

    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty");

        Name = name;
    }

    public void SetUrlImage(string? urlImage)
    {
        if (string.IsNullOrWhiteSpace(urlImage))
            throw new ArgumentException("SetUrlImage cannot be empty");

        UrlImage = urlImage;
    }

    public void SetIngredients(string ingredients)
    {
        if (string.IsNullOrWhiteSpace(ingredients))
            throw new ArgumentException("Ingredients cannot be empty");

        Ingredients = ingredients;
    }

    public void SetCategoryId(int categoryId)
    {
        if (categoryId < 1)
            throw new ArgumentException("CategoryId cannot be negative");

        CategoryId = categoryId;
    }

    public void SetUserId(int userId)
    {
        if (userId < 1)
            throw new ArgumentException("UserId cannot be negative");

        UserId = userId;
    }

    public void AddStage(Stage stage)
    {
        if (stage == null)
            throw new ArgumentException("Ingredients cannot be null");
        
        Stages.Add(stage);
    }
    public void AddRangeStage(List<Stage> stages)
    {
        if (stages == null || stages.Count == 0)
            throw new ArgumentException("Ingredients cannot be null");
        
        Stages.AddRange(stages);
    }
}