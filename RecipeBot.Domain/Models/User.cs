namespace MyRecipeDomain.Models;

public class User
{
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public string Name { get; set; }
    public string HandlerType { get; set; }
    public string State { get; set; }
    public virtual List<Recipe>? Recipes { get; set; }
}