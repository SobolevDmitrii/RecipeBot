namespace RecipeBot.Application.Models;

public class RecipeDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? UrlImage { get; set; }
    public int CategoryId { get; set; }
    public string Ingredients { get; set; } = string.Empty;
    public List<StageDto> Stages { get; set; } = [];
}