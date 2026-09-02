using Microsoft.EntityFrameworkCore;
using MyRecipeDomain.Models;

namespace RecipeBot.Infrastructure;

public class MyRecipeDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Recipe> Recipes { get; set; }
    public DbSet<Stage> Stages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MyRecipeDbContext).Assembly);
    }
}