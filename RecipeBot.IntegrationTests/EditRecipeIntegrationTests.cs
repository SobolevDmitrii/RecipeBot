using AutoFixture;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeBot.Application.Abstract;
using RecipeBot.Application.Handlers;
using RecipeBot.Application.Models;
using RecipeBot.Domain.Abstract.IRepositories;
using RecipeBot.Domain.Enums;
using RecipeBot.Domain.Models;
using RecipeBot.Infrastructure;
using RecipeBot.Infrastructure.Redis;
using RecipeBot.Infrastructure.Repositories;
using RecipeBot.IntegrationTests.Repositories;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace RecipeBot.IntegrationTests;

[TestFixture]
public class EditRecipeIntegrationTests : RepositoryTestBase
{
    private RedisContainer? _redis;
    private ConnectionMultiplexer? _connection;
    private ServiceProvider? _services;

    [SetUp]
    public async Task StartRedis()
    {
        _redis = new RedisBuilder("redis:7-alpine").Build();
        try
        {
            await _redis.StartAsync();
            _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(Factory);
            services.AddSingleton<IConnectionMultiplexer>(_connection);
            services.AddSingleton<IRedisService, RedisService>();
            services.AddSingleton<IUserSessionStore, UserSessionsStore>();
            services.AddScoped<IUsersRepository, UsersRepository>();
            services.AddScoped<IRecipeRepository, RecipeRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddMediatR(configuration =>
                configuration.RegisterServicesFromAssemblyContaining<EditRecipeHandler>());
            _services = services.BuildServiceProvider();
        }
        catch
        {
            await StopRedis();
            throw;
        }
    }

    [TearDown]
    public async Task StopRedis()
    {
        try
        {
            if (_services is not null)
                await _services.DisposeAsync();
        }
        finally
        {
            try
            {
                _connection?.Dispose();
            }
            finally
            {
                if (_redis is not null)
                    await _redis.DisposeAsync();
            }
        }
    }


    [Test]
    public async Task EditRecipe_ChangesFieldsOnlyAfterSave()
    {
        // Arrange
        var data = await SeedOwners();
        var recipe = await CreateRecipe(data.Owner.Id, data.Category.Id);
        var otherRecipe = await CreateRecipe(data.Other.Id, data.Category.Id);
        var name = Fixture.Create<string>();
        var ingredients = Fixture.Create<string>();
        var image = Fixture.Create<string>();
        await BeginEdit(data.Owner.TelegramId, recipe.Id);

        // Act
        await Send(data.Owner.TelegramId, callback: "edit:name");
        await Send(data.Owner.TelegramId, text: name);
        await Send(data.Owner.TelegramId, callback: "edit:ingredients");
        await Send(data.Owner.TelegramId, text: ingredients);
        await Send(data.Owner.TelegramId, callback: "edit:category");
        await Send(data.Owner.TelegramId, callback: data.OtherCategory.Id.ToString());
        await Send(data.Owner.TelegramId, callback: "edit:image");
        await Send(data.Owner.TelegramId, imageFileId: image);

        // Assert
        await AssertRecipeUnchanged(recipe);
        var draft = (await ReadSession(data.Owner.TelegramId))!.RecipeDraft!;
        Assert.Multiple(() =>
        {
            Assert.That(draft.Name, Is.EqualTo(name));
            Assert.That(draft.Ingredients, Is.EqualTo(ingredients));
            Assert.That(draft.CategoryId, Is.EqualTo(data.OtherCategory.Id));
            Assert.That(draft.UrlImage, Is.EqualTo(image));
        });
        var result = await Send(data.Owner.TelegramId, callback: "edit:save");
        var saved = await ReadRecipe(recipe.Id);
        var session = await ReadSession(data.Owner.TelegramId);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Message, Is.EqualTo("✅ Изменения рецепта сохранены."));
            Assert.That(saved.UserId, Is.EqualTo(data.Owner.Id));
            Assert.That(saved.Name, Is.EqualTo(name));
            Assert.That(saved.Ingredients, Is.EqualTo(ingredients));
            Assert.That(saved.CategoryId, Is.EqualTo(data.OtherCategory.Id));
            Assert.That(saved.UrlImage, Is.EqualTo(image));
            Assert.That(saved.Stages.Select(s => (s.Number, s.Description, s.UrlImage)),
                Is.EqualTo(recipe.Stages.Select(s => (s.Number, s.Description, s.UrlImage))));
            Assert.That(session!.UserState, Is.EqualTo(UserStates.GetMyRecipes));
            Assert.That(session.RecipeState, Is.EqualTo(RecipeStates.ShowRecipeSelectionOptions));
        });
        await AssertRecipeUnchanged(otherRecipe);
    }

    [Test]
    public async Task EditStages_UpdatesInsertsAndDeletesStages()
    {
        // Arrange
        var data = await SeedOwners();
        var recipe = await CreateRecipe(data.Owner.Id, data.Category.Id);
        var description = Fixture.Create<string>();
        var image = Fixture.Create<string>();
        var newDescription = Fixture.Create<string>();
        var newImage = Fixture.Create<string>();
        await BeginEdit(data.Owner.TelegramId, recipe.Id);

        // Act
        await Send(data.Owner.TelegramId, callback: "edit:stages");
        await Send(data.Owner.TelegramId, callback: "edit:stage:1");
        await Send(data.Owner.TelegramId, callback: "edit:stage:text");
        await Send(data.Owner.TelegramId, text: description);
        await Send(data.Owner.TelegramId, callback: "edit:stage:photo");
        await Send(data.Owner.TelegramId, imageFileId: image);
        await Send(data.Owner.TelegramId, callback: "edit:stage:save");
        await Send(data.Owner.TelegramId, callback: "edit:stages:add");
        await Send(data.Owner.TelegramId, caption: $"1\n{newDescription}", imageFileId: newImage);
        await Send(data.Owner.TelegramId, callback: "edit:stage:3");
        await Send(data.Owner.TelegramId, callback: "edit:stage:delete");

        // Assert
        await AssertRecipeUnchanged(recipe);
        var session = await ReadSession(data.Owner.TelegramId);
        var expectedStages = new[] { (1, newDescription, newImage), (2, description, image) };
        Assert.That(session!.RecipeDraft!.Stages.Select(s => (s.Number, s.Description, s.UrlImage)),
            Is.EqualTo(expectedStages));
        await Send(data.Owner.TelegramId, callback: "edit:save");
        var saved = await ReadRecipe(recipe.Id);
        Assert.That(saved.Stages.Select(s => (s.Number, s.Description, s.UrlImage)), Is.EqualTo(expectedStages));
        await using var db = await Factory.CreateDbContextAsync();
        Assert.That(await db.Stages.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task RemoveImages_PersistsRecipeAndStageWithoutImages()
    {
        // Arrange
        var data = await SeedOwners();
        var recipe = await CreateRecipe(data.Owner.Id, data.Category.Id);
        await BeginEdit(data.Owner.TelegramId, recipe.Id);

        // Act
        await Send(data.Owner.TelegramId, callback: "edit:remove_image");
        await Send(data.Owner.TelegramId, callback: "edit:stages");
        await Send(data.Owner.TelegramId, callback: "edit:stage:1");
        await Send(data.Owner.TelegramId, callback: "edit:stage:delete:photo");
        await Send(data.Owner.TelegramId, callback: "edit:stage:save");
        await Send(data.Owner.TelegramId, callback: "edit:save");

        // Assert
        var saved = await ReadRecipe(recipe.Id);
        Assert.Multiple(() =>
        {
            Assert.That(saved.UrlImage, Is.Null);
            Assert.That(saved.Stages[0].UrlImage, Is.Null);
            Assert.That(saved.Stages[0].Description, Is.EqualTo(recipe.Stages[0].Description));
            Assert.That(saved.Stages[1].UrlImage, Is.EqualTo(recipe.Stages[1].UrlImage));
        });
    }

    [Test]
    public async Task CancelEdit_DiscardsChangesWhenRecipeIsOpenedAgain()
    {
        // Arrange
        var data = await SeedOwners();
        var recipe = await CreateRecipe(data.Owner.Id, data.Category.Id);
        await BeginEdit(data.Owner.TelegramId, recipe.Id);
        await Send(data.Owner.TelegramId, callback: "edit:name");
        await Send(data.Owner.TelegramId, text: Fixture.Create<string>());
        await Send(data.Owner.TelegramId, callback: "edit:stages");
        await Send(data.Owner.TelegramId, callback: "edit:stage:1");
        await Send(data.Owner.TelegramId, callback: "edit:stage:delete");

        // Act
        var result = await Send(data.Owner.TelegramId, callback: "edit:cancel");

        // Assert
        await AssertRecipeUnchanged(recipe);
        var session = await ReadSession(data.Owner.TelegramId);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Message, Is.EqualTo("Редактирование отменено. Рецепт не изменён."));
            Assert.That(session!.UserState, Is.EqualTo(UserStates.GetMyRecipes));
            Assert.That(session.RecipeState, Is.EqualTo(RecipeStates.ShowRecipeSelectionOptions));
        });
        await BeginEdit(data.Owner.TelegramId, recipe.Id);
        var reopened = (await ReadSession(data.Owner.TelegramId))!.RecipeDraft!;
        Assert.Multiple(() =>
        {
            Assert.That(reopened.Name, Is.EqualTo(recipe.Name));
            Assert.That(reopened.Stages.Select(s => (s.Number, s.Description, s.UrlImage)),
                Is.EqualTo(recipe.Stages.Select(s => (s.Number, s.Description, s.UrlImage))));
        });
    }

    [Test]
    public async Task BackToMenu_KeepsDraftForSaving()
    {
        // Arrange
        var data = await SeedOwners();
        var recipe = await CreateRecipe(data.Owner.Id, data.Category.Id);
        var name = Fixture.Create<string>();
        await BeginEdit(data.Owner.TelegramId, recipe.Id);
        await Send(data.Owner.TelegramId, callback: "edit:name");
        await Send(data.Owner.TelegramId, text: name);
        await Send(data.Owner.TelegramId, callback: "edit:ingredients");

        // Act
        var result = await Send(data.Owner.TelegramId, callback: "edit:back");

        // Assert
        var session = await ReadSession(data.Owner.TelegramId);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Buttons!.InlineKeyboard.SelectMany(row => row)
                .Select(button => button.CallbackData), Does.Contain("edit:save"));
            Assert.That(session!.UserState, Is.EqualTo(UserStates.EditRecipe));
            Assert.That(session.RecipeState, Is.EqualTo(RecipeStates.WaitingMove));
            Assert.That(session.RecipeDraft!.Name, Is.EqualTo(name));
            Assert.That(session.RecipeDraft.Ingredients, Is.EqualTo(recipe.Ingredients));
        });
        await AssertRecipeUnchanged(recipe);
        await Send(data.Owner.TelegramId, callback: "edit:save");
        Assert.That((await ReadRecipe(recipe.Id)).Name, Is.EqualTo(name));
    }

    [Test]
    public async Task SaveWithoutStages_KeepsDraftAndOriginalRecipe()
    {
        // Arrange
        var data = await SeedOwners();
        var recipe = await CreateRecipe(data.Owner.Id, data.Category.Id);
        await BeginEdit(data.Owner.TelegramId, recipe.Id);
        await Send(data.Owner.TelegramId, callback: "edit:stages");
        for (var i = 0; i < 2; i++)
        {
            await Send(data.Owner.TelegramId, callback: "edit:stage:1");
            await Send(data.Owner.TelegramId, callback: "edit:stage:delete");
        }

        // Act
        var result = await Send(data.Owner.TelegramId, callback: "edit:save");

        // Assert
        Assert.That(result!.Message,
            Is.EqualTo("Заполните название, ингредиенты и хотя бы один этап с описанием."));
        await AssertRecipeUnchanged(recipe);
        var session = await ReadSession(data.Owner.TelegramId);
        Assert.Multiple(() =>
        {
            Assert.That(session!.UserState, Is.EqualTo(UserStates.EditRecipe));
            Assert.That(session.RecipeDraft!.Stages, Is.Empty);
        });
    }

    [Test]
    public async Task SaveDeletedRecipe_KeepsDraftInRedis()
    {
        // Arrange
        var data = await SeedOwners();
        var recipe = await CreateRecipe(data.Owner.Id, data.Category.Id);
        var name = Fixture.Create<string>();
        await BeginEdit(data.Owner.TelegramId, recipe.Id);
        await Send(data.Owner.TelegramId, callback: "edit:name");
        await Send(data.Owner.TelegramId, text: name);
        await using (var db = await Factory.CreateDbContextAsync())
            await db.Recipes.Where(r => r.Id == recipe.Id).ExecuteDeleteAsync();

        // Act
        var result = await Send(data.Owner.TelegramId, callback: "edit:save");

        // Assert
        Assert.That(result!.Message,
            Is.EqualTo("Рецепт больше недоступен. Изменения не сохранены; черновик сохранён в сессии."));
        var session = await ReadSession(data.Owner.TelegramId);
        Assert.Multiple(() =>
        {
            Assert.That(session!.UserState, Is.EqualTo(UserStates.EditRecipe));
            Assert.That(session.RecipeDraft!.Name, Is.EqualTo(name));
        });
        await using var check = await Factory.CreateDbContextAsync();
        Assert.That(await check.Recipes.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task SaveDeletedCategory_RequiresAnotherCategory()
    {
        // Arrange
        var data = await SeedOwners();
        var recipe = await CreateRecipe(data.Owner.Id, data.Category.Id);
        await BeginEdit(data.Owner.TelegramId, recipe.Id);
        await Send(data.Owner.TelegramId, callback: "edit:category");
        await Send(data.Owner.TelegramId, callback: data.OtherCategory.Id.ToString());
        await using (var db = await Factory.CreateDbContextAsync())
            await db.Categories.Where(c => c.Id == data.OtherCategory.Id).ExecuteDeleteAsync();

        // Act
        var result = await Send(data.Owner.TelegramId, callback: "edit:save");

        // Assert
        Assert.That(result!.Message, Is.EqualTo("Категория больше недоступна. Выберите другую категорию."));
        await AssertRecipeUnchanged(recipe);
        var session = await ReadSession(data.Owner.TelegramId);
        Assert.Multiple(() =>
        {
            Assert.That(session!.UserState, Is.EqualTo(UserStates.EditRecipe));
            Assert.That(session.RecipeDraft!.CategoryId, Is.EqualTo(data.OtherCategory.Id));
        });
    }

    [Test]
    public async Task OpenOtherUsersRecipe_DoesNotLoadDraft()
    {
        // Arrange
        var data = await SeedOwners();
        var recipe = await CreateRecipe(data.Other.Id, data.Category.Id);

        // Act
        var result = await BeginEdit(data.Owner.TelegramId, recipe.Id);

        // Assert
        Assert.That(result!.Message, Is.EqualTo("Рецепт не найден. Выберите его снова."));
        Assert.That((await ReadSession(data.Owner.TelegramId))!.RecipeDraft, Is.Null);
        await AssertRecipeUnchanged(recipe);
    }

    [Test]
    public async Task SaveOtherUsersRecipe_DoesNotChangeDatabase()
    {
        // Arrange
        var data = await SeedOwners();
        var recipe = await CreateRecipe(data.Other.Id, data.Category.Id);
        await BeginEdit(data.Other.TelegramId, recipe.Id);
        var draftSession = (await ReadSession(data.Other.TelegramId))!;
        draftSession.RecipeDraft!.Name = Fixture.Create<string>();
        await _services!.GetRequiredService<IUserSessionStore>()
            .SetUserSession(data.Owner.TelegramId, draftSession);

        // Act
        var result = await Send(data.Owner.TelegramId, callback: "edit:save");

        // Assert
        Assert.That(result!.Message,
            Is.EqualTo("Рецепт больше недоступен. Изменения не сохранены; черновик сохранён в сессии."));
        await AssertRecipeUnchanged(recipe);
    }

    #region Helpers

    private int _updateId;

    private async Task<Recipe> CreateRecipe(int userId, int categoryId)
    {
        var recipe = NewRecipe(userId, categoryId);
        for (var number = 1; number <= 2; number++)
        {
            var stage = NewStage();
            stage.SetNumber(number);
            recipe.AddStage(stage);
        }
        await SaveRecipes(recipe);
        return recipe;
    }

    private async Task<HandlerResult?> BeginEdit(long chatId, int recipeId)
    {
        await _services!.GetRequiredService<IUserSessionStore>().SetUserSession(chatId, new UserSession
        {
            ChatId = chatId,
            UserState = UserStates.EditRecipe,
            RecipeState = RecipeStates.GetMenuEdit
        });
        return await Send(chatId, callback: $"recipe:edit:{recipeId}");
    }

    private async Task<HandlerResult?> Send(long chatId, string? text = null, string? callback = null,
        string? imageFileId = null, string? caption = null)
    {
        using var scope = _services!.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
            new EditRecipeRequest(chatId, text, imageFileId, callback, caption, ++_updateId));
    }

    private async Task<UserSession?> ReadSession(long chatId)
    {
        using var reader = await ConnectionMultiplexer.ConnectAsync(_redis!.GetConnectionString());
        return await new UserSessionsStore(new RedisService(reader)).TryGetSession(chatId);
    }

    private async Task<Recipe> ReadRecipe(int id)
    {
        await using var db = await Factory.CreateDbContextAsync();
        return await db.Recipes.AsNoTracking().Include(r => r.Stages.OrderBy(s => s.Number))
            .SingleAsync(r => r.Id == id);
    }

    private async Task AssertRecipeUnchanged(Recipe expected)
    {
        var saved = await ReadRecipe(expected.Id);
        Assert.Multiple(() =>
        {
            Assert.That(saved.UserId, Is.EqualTo(expected.UserId));
            Assert.That(saved.Name, Is.EqualTo(expected.Name));
            Assert.That(saved.Ingredients, Is.EqualTo(expected.Ingredients));
            Assert.That(saved.CategoryId, Is.EqualTo(expected.CategoryId));
            Assert.That(saved.UrlImage, Is.EqualTo(expected.UrlImage));
            Assert.That(saved.Stages.Select(s => (s.Number, s.Description, s.UrlImage)),
                Is.EqualTo(expected.Stages.Select(s => (s.Number, s.Description, s.UrlImage))));
        });
    }

    #endregion
}
