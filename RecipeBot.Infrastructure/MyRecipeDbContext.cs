using Microsoft.EntityFrameworkCore;
using MyRecipeDomain.Models;

namespace RecipeBot.Infrastructure;

public class MyRecipeDbContext(DbContextOptions<MyRecipeDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Recipe> Recipes { get; set; }
    public DbSet<Stage> Stages { get; set; }
    public DbSet<Category> Categories {get; set;}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MyRecipeDbContext).Assembly);
    }
}