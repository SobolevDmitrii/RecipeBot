using AutoFixture;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using RecipeBot.Domain.Models;
using RecipeBot.Infrastructure;
using Testcontainers.PostgreSql;

namespace RecipeBot.IntegrationTests.Repositories;

[NonParallelizable]
public abstract class RepositoryTestBase
{
    private PostgreSqlContainer? _postgres;
    protected IDbContextFactory<MyRecipeDbContext> Factory = null!;
    protected Fixture Fixture = null!;

    [OneTimeSetUp]
    public async Task StartDatabase()
    {
        _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        try
        {
            await _postgres.StartAsync();
            Factory = new PooledDbContextFactory<MyRecipeDbContext>(
                new DbContextOptionsBuilder<MyRecipeDbContext>()
                    .UseNpgsql(_postgres.GetConnectionString()).Options);
            await using var db = await Factory.CreateDbContextAsync();
            await db.Database.MigrateAsync();
        }
        catch
        {
            await _postgres.DisposeAsync();
            throw;
        }
    }

    [SetUp]
    public async Task ResetDatabase()
    {
        Fixture = new Fixture();
        // Только отдельная контейнерная БД. Порядок учитывает внешние ключи.
        await using var db = await Factory.CreateDbContextAsync();
        await db.Stages.ExecuteDeleteAsync();
        await db.Recipes.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        await db.Categories.ExecuteDeleteAsync();
    }

    [OneTimeTearDown]
    public async Task StopDatabase()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    protected User NewUser()
    {
        var user = new User();
        user.SetTelegramId(Fixture.Create<long>());
        user.SetName(Fixture.Create<string>());
        return user;
    }

    protected Category NewCategory()
    {
        var category = new Category();
        category.SetName(Fixture.Create<string>());
        return category;
    }

    protected Recipe NewRecipe(int userId, int categoryId)
    {
        var recipe = new Recipe();
        recipe.SetUserId(userId);
        recipe.SetCategoryId(categoryId);
        recipe.SetName(Fixture.Create<string>());
        recipe.SetIngredients(Fixture.Create<string>());
        recipe.SetUrlImage(Fixture.Create<string>());
        return recipe;
    }

    protected Stage NewStage()
    {
        var stage = new Stage();
        stage.SetDescription(Fixture.Create<string>());
        stage.SetNumber(Fixture.Create<int>());
        stage.SetUrlImage(Fixture.Create<string>());
        return stage;
    }

    protected async Task<(User Owner, User Other, Category Category, Category OtherCategory)> SeedOwners()
    {
        var owner = NewUser();
        var other = NewUser();
        var category = NewCategory();
        var otherCategory = NewCategory();
        await using var db = await Factory.CreateDbContextAsync();
        db.Users.AddRange(owner, other);
        db.Categories.AddRange(category, otherCategory);
        await db.SaveChangesAsync();
        return (owner, other, category, otherCategory);
    }

    protected async Task SaveRecipes(params Recipe[] recipes)
    {
        await using var db = await Factory.CreateDbContextAsync();
        db.Recipes.AddRange(recipes);
        await db.SaveChangesAsync();
    }
}
