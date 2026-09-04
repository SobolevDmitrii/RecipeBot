namespace MyRecipeDomain.Models;

public class Category
{
    public int Id { get; private set; }
    public string Name { get; private set; }
    
    public virtual List<Recipe> Recipes { get; set; }

    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty");

        Name = name;
    }
}