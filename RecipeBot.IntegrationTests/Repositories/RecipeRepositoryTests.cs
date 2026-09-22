using AutoFixture;
using Microsoft.EntityFrameworkCore;
using RecipeBot.Infrastructure.Repositories;

namespace RecipeBot.IntegrationTests.Repositories;

[TestFixture]
public class RecipeRepositoryTests : RepositoryTestBase
{
    private RecipeRepository Repository => new(Factory, new UsersRepository(Factory));

    [Test]
    public async Task Add()
    {
        var data = await SeedOwners();
        var recipe = NewRecipe(data.Other.Id, data.Category.Id);
        var stage = NewStage();
        recipe.AddStage(stage);

        var result = await Repository.Add(recipe, data.Owner.TelegramId, CancellationToken.None);

        await using var db = await Factory.CreateDbContextAsync();
        var saved = await db.Recipes.Include(r => r.Stages).SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.GreaterThan(0));
            Assert.That(saved.Id, Is.EqualTo(result.Id));
            Assert.That(saved.UserId, Is.EqualTo(data.Owner.Id));
            Assert.That(saved.CategoryId, Is.EqualTo(data.Category.Id));
            Assert.That(saved.Name, Is.EqualTo(recipe.Name));
            Assert.That(saved.Ingredients, Is.EqualTo(recipe.Ingredients));
            Assert.That(saved.UrlImage, Is.EqualTo(recipe.UrlImage));
            Assert.That(saved.Stages.Select(s => (s.Description, s.Number, s.UrlImage)),
                Is.EquivalentTo(new[] { (stage.Description, stage.Number, stage.UrlImage) }));
        });
    }

    [Test]
    public async Task GetListByTelegramId()
    {
        var data = await SeedOwners();
        var first = NewRecipe(data.Owner.Id, data.Category.Id);
        var second = NewRecipe(data.Owner.Id, data.OtherCategory.Id);
        await SaveRecipes(first, second, NewRecipe(data.Other.Id, data.Category.Id));

        var result = await Repository.GetListByTelegramId(data.Owner.TelegramId, CancellationToken.None);

        Assert.That(result.Select(r => (r.Id, r.Name)),
            Is.EquivalentTo(new[] { (first.Id, first.Name), (second.Id, second.Name) }));
    }

    [Test]
    public async Task GetListByTelegramIdReturnsEmpty()
    {
        var data = await SeedOwners();
        await SaveRecipes(NewRecipe(data.Other.Id, data.Category.Id));
        var result = await Repository.GetListByTelegramId(data.Owner.TelegramId, CancellationToken.None);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetListByTelegramIdAndCategory()
    {
        var data = await SeedOwners();
        var expected = NewRecipe(data.Owner.Id, data.Category.Id);
        await SaveRecipes(expected, NewRecipe(data.Owner.Id, data.OtherCategory.Id),
            NewRecipe(data.Other.Id, data.Category.Id));

        var result = await Repository.GetListByTelegramIdAndCategory(
            data.Owner.TelegramId, data.Category.Id, CancellationToken.None);

        Assert.That(result.Select(r => r.Id), Is.EqualTo(new[] { expected.Id }));
    }

    [Test]
    public async Task GetListByTelegramIdAndCategoryReturnsEmpty()
    {
        var data = await SeedOwners();
        await SaveRecipes(NewRecipe(data.Owner.Id, data.OtherCategory.Id),
            NewRecipe(data.Other.Id, data.Category.Id));
        var result = await Repository.GetListByTelegramIdAndCategory(
            data.Owner.TelegramId, data.Category.Id, CancellationToken.None);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task SearchByNameAndFiltersOwner()
    {
        var data = await SeedOwners();
        var term = Fixture.Create<string>().ToUpperInvariant();
        var matching = NewRecipe(data.Owner.Id, data.Category.Id);
        matching.SetName($"{Fixture.Create<string>()} {term} {Fixture.Create<string>()}");
        var foreign = NewRecipe(data.Other.Id, data.Category.Id);
        foreign.SetName(matching.Name);
        await SaveRecipes(matching, foreign, NewRecipe(data.Owner.Id, data.Category.Id));

        var result = await Repository.SearchByName(
            data.Owner.TelegramId, term.ToLowerInvariant(), CancellationToken.None);

        Assert.That(result.Select(r => r.Id), Is.EqualTo(new[] { matching.Id }));
    }

    [Test]
    public async Task SearchByNameReturnEmpty()
    {
        var data = await SeedOwners();
        var recipe = NewRecipe(data.Owner.Id, data.Category.Id);
        await SaveRecipes(recipe);
        // Более длинная строка заведомо не является подстрокой имени.
        var result = await Repository.SearchByName(data.Owner.TelegramId,
            recipe.Name + Fixture.Create<string>(), CancellationToken.None);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task Get()
    {
        var data = await SeedOwners();
        var recipe = NewRecipe(data.Owner.Id, data.Category.Id);
        recipe.AddStage(NewStage());
        recipe.AddStage(NewStage());
        await SaveRecipes(recipe);

        var result = await Repository.Get(data.Owner.TelegramId, recipe.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(recipe.Id));
            Assert.That(result.Name, Is.EqualTo(recipe.Name));
            Assert.That(result.Ingredients, Is.EqualTo(recipe.Ingredients));
            Assert.That(result.UrlImage, Is.EqualTo(recipe.UrlImage));
            Assert.That(result.User.TelegramId, Is.EqualTo(data.Owner.TelegramId));
            Assert.That(result.Category.Name, Is.EqualTo(data.Category.Name));
            Assert.That(result.Stages.Select(s => (s.Id, s.Description, s.Number, s.UrlImage)),
                Is.EquivalentTo(recipe.Stages.Select(s => (s.Id, s.Description, s.Number, s.UrlImage))));
        });
    }

    [Test]
    public async Task GetReturnException()
    {
        var data = await SeedOwners();
        var recipe = NewRecipe(data.Other.Id, data.Category.Id);
        await SaveRecipes(recipe);
        Assert.ThrowsAsync<InvalidOperationException>(() => Repository.Get(
            data.Owner.TelegramId, recipe.Id, CancellationToken.None));
    }

    [Test]
    public async Task Delete()
    {
        var data = await SeedOwners();
        var recipe = NewRecipe(data.Owner.Id, data.Category.Id);
        recipe.AddStage(NewStage());
        var other = NewRecipe(data.Other.Id, data.Category.Id);
        other.AddStage(NewStage());
        await SaveRecipes(recipe, other);

        await Repository.Delete(data.Owner.TelegramId, recipe.Id, CancellationToken.None);

        await using var db = await Factory.CreateDbContextAsync();
        Assert.That(await db.Recipes.Select(r => r.Id).ToListAsync(), Is.EqualTo(new[] { other.Id }));
        Assert.That(await db.Stages.Select(s => s.RecipeId).ToListAsync(), Is.EqualTo(new[] { other.Id }));
        Assert.That(await db.Users.CountAsync(), Is.EqualTo(2));
        Assert.That(await db.Categories.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task DeleteReturnException()
    {
        var data = await SeedOwners();
        var recipe = NewRecipe(data.Other.Id, data.Category.Id);
        recipe.AddStage(NewStage());
        await SaveRecipes(recipe);

        Assert.ThrowsAsync<InvalidOperationException>(() => Repository.Delete(
            data.Owner.TelegramId, recipe.Id, CancellationToken.None));

        await using var db = await Factory.CreateDbContextAsync();
        Assert.That(await db.Recipes.Select(r => r.Id).ToListAsync(), Is.EqualTo(new[] { recipe.Id }));
        Assert.That(await db.Stages.CountAsync(), Is.EqualTo(1));
    }
}
