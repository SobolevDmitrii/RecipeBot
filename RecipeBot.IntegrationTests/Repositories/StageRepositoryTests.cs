using AutoFixture;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RecipeBot.Infrastructure.Repositories;

namespace RecipeBot.IntegrationTests.Repositories;

[TestFixture]
public class StageRepositoryTests : RepositoryTestBase
{
    [Test]
    public async Task Add()
    {
        var data = await SeedOwners();
        var recipe = NewRecipe(data.Owner.Id, data.Category.Id);
        await SaveRecipes(recipe);
        var stage = NewStage();
        stage.SetRecipe(recipe.Id);

        var result = await new StageRepository(Factory).Add(
            stage, data.Owner.TelegramId, CancellationToken.None);

        await using var db = await Factory.CreateDbContextAsync();
        var saved = await db.Stages.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.GreaterThan(0));
            Assert.That(saved.Id, Is.EqualTo(result.Id));
            Assert.That(saved.RecipeId, Is.EqualTo(recipe.Id));
            Assert.That(saved.Description, Is.EqualTo(stage.Description));
            Assert.That(saved.Number, Is.EqualTo(stage.Number));
            Assert.That(saved.UrlImage, Is.EqualTo(stage.UrlImage));
        });
    }

    [Test]
    public async Task AddForMissingRecipeReturnSqlException()
    {
        var stage = NewStage();
        stage.SetRecipe(Fixture.Create<int>());

        var error = Assert.ThrowsAsync<DbUpdateException>(() => new StageRepository(Factory)
            .Add(stage, Fixture.Create<long>(), CancellationToken.None));

        Assert.That(error!.InnerException, Is.TypeOf<PostgresException>());
        Assert.That(((PostgresException)error.InnerException!).SqlState,
            Is.EqualTo(PostgresErrorCodes.ForeignKeyViolation));
        await using var db = await Factory.CreateDbContextAsync();
        Assert.That(await db.Stages.CountAsync(), Is.Zero);
    }
}
