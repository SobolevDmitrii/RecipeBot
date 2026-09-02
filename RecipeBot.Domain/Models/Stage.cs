namespace MyRecipeDomain.Models;

public class Stage
{
    public int Id { get; set; }
    public string Description { get; set; }
    public int Number { get; set; }
    public string? UrlImage { get; set; }
    public int RecipeId { get; set; }
    public virtual Recipe Recipe { get; set; }
}