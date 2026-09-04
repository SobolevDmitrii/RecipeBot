namespace MyRecipeDomain.Models;

public class Recipe
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? UrlImage { get; set; }
    public string Ingredients { get; set; }
    public virtual List<Stage> Stages { get; set; }
    public virtual User User { get; set; }
    public virtual Category Category { get; set; }
    public int CategoryId { get; set; }
    public int UserId { get; set; }
}