namespace MyRecipeDomain.Models;

public class User
{
    public int Id { get; private set; }
    public long TelegramId { get; private set; }
    public string Name { get; private set; }

    public string? State { get; private set; }
    public virtual List<Recipe>? Recipes { get; private set; } = [];

    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty");
        Name = name;
    }

    public void SetState(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("State cannot be empty");
        State = state;
    }

    public void SetTelegramId(long telegramId)
    {
        if (telegramId <= 0)
            throw new ArgumentException("TelegramId cannot be zero or negative");
        TelegramId = telegramId;
    }

    public void AddRecipe(Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        Recipes.Add(recipe);
    }
}