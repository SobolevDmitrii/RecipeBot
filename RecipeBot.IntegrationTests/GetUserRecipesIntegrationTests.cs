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
using Telegram.Bot.Types.ReplyMarkups;
using Testcontainers.Redis;

namespace RecipeBot.IntegrationTests;

[TestFixture]
public class GetUserRecipesIntegrationTests : RepositoryTestBase
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
            _connection = await ConnectionMultiplexer.ConnectAsync(
                _redis.GetConnectionString());

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
                configuration.RegisterServicesFromAssemblyContaining<GetUserRecipesHandler>());
            _services = services.BuildServiceProvider();
        }
        catch
        {
            await StopRedis();
            throw;
        }
    }

    [Test]
    public async Task GetAllRecipesTest()
    {
        const int pageSize = 5;
        var chatId = Fixture.Create<long>();
        var otherChatId = Fixture.Create<long>();
        var updateId = Fixture.Create<int>();
        var expected = new List<Recipe>();

        await CreateDataForDB(chatId, otherChatId, expected);

        var sessionStore = _services!.GetRequiredService<IUserSessionStore>();
        await sessionStore.SetUserSession(chatId, new UserSession
        {
            ChatId = chatId,
            UserState = UserStates.GetMyRecipes,
            RecipeState = RecipeStates.WaitingMove
        });

        var firstPage = await Send(
            new GetUserRecipesRequest(chatId, null, "all_list", updateId),
            CancellationToken.None);

        Assert.That(firstPage, Is.Not.Null);
        Assert.That(firstPage!.Buttons, Is.Not.Null);
        var firstButtons = firstPage.Buttons!.InlineKeyboard.SelectMany(row => row).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(firstPage.Message, Is.EqualTo("Выбери рецепт"));
            Assert.That(RecipeButtons(firstButtons), Has.Length.EqualTo(Math.Min(pageSize, expected.Count)));
            Assert.That(firstButtons.Any(b => b.CallbackData == "recipes:page:1"),
                Is.EqualTo(expected.Count > pageSize));
        });

        using var reader = await ConnectionMultiplexer.ConnectAsync(_redis!.GetConnectionString());
        var saved = await new UserSessionsStore(new RedisService(reader)).TryGetSession(chatId);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.Recipes, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(saved.RecipeState, Is.EqualTo(RecipeStates.ShowRecipes));
            Assert.That(saved.CurrentUpdateId, Is.EqualTo(updateId));
            Assert.That(saved.Recipes!.Select(r => (r.Id, r.Name)),
                Is.EquivalentTo(expected.Select(r => (r.Id, r.Name))));
        });

        var displayedRecipes = RecipeButtons(firstButtons)
            .Select(b => (b.CallbackData, b.Text)).ToList();

        var pageCount = (int)Math.Ceiling(expected.Count / (double)pageSize);
        for (var pageIndex = 1; pageIndex < pageCount; pageIndex++)
        {
            var page = await Send(
                new GetUserRecipesRequest(chatId, null, $"recipes:page:{pageIndex}", updateId + pageIndex),
                CancellationToken.None);

            Assert.That(page, Is.Not.Null, $"Страница {pageIndex + 1}");
            Assert.That(page!.Buttons, Is.Not.Null);
            var buttons = page.Buttons!.InlineKeyboard.SelectMany(row => row).ToArray();
            var recipesOnPage = RecipeButtons(buttons);
            Assert.Multiple(() =>
            {
                Assert.That(recipesOnPage,
                    Has.Length.EqualTo(Math.Min(pageSize, expected.Count - pageIndex * pageSize)),
                    $"Количество рецептов на странице {pageIndex + 1}");
                Assert.That(buttons.Any(b => b.CallbackData == $"recipes:page:{pageIndex - 1}"), Is.True);
                Assert.That(buttons.Any(b => b.CallbackData == $"recipes:page:{pageIndex + 1}"),
                    Is.EqualTo(pageIndex < pageCount - 1));
            });
            displayedRecipes.AddRange(recipesOnPage.Select(b => (b.CallbackData, b.Text)));
        }

        Assert.That(displayedRecipes,
            Is.EquivalentTo(expected.Select(r => ($"recipe:{r.Id}", $"📖 {r.Name}"))));
    }

    [Test]
    public async Task GetRecipesByCategoryTest()
    {
        var chatId = Fixture.Create<long>();
        var otherChatId = Fixture.Create<long>();
        var updateId = Fixture.Create<int>();
        var nextUpdateId = updateId + 1;
        var expected = new List<Recipe>();

        await CreateDataForDB(chatId, otherChatId, expected);

        var sessionStore = _services!.GetRequiredService<IUserSessionStore>();
        await sessionStore.SetUserSession(chatId, new UserSession
        {
            ChatId = chatId,
            UserState = UserStates.GetMyRecipes,
            RecipeState = RecipeStates.WaitingMove
        });

        var categoryPage = await Send(
            new GetUserRecipesRequest(chatId, null, "for_category", updateId),
            CancellationToken.None);

        Assert.That(categoryPage, Is.Not.Null);
        Assert.That(categoryPage!.Buttons, Is.Not.Null);
        var buttons = categoryPage.Buttons!.InlineKeyboard.SelectMany(row => row).ToArray();
        var categories = new List<Category>();
        await using (var db = await Factory.CreateDbContextAsync())
        {
            categories = await db.Categories.ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(categoryPage.Message, Is.EqualTo("✅ Выберите категорию."));
                Assert.That(buttons, Has.Length.EqualTo(categories.Count));
            });

            var category = categories[Random.Shared.Next(categories.Count)];
            var expectedInCategory = expected.Where(r => r.CategoryId == category.Id).ToArray();

            var page = await Send(
                new GetUserRecipesRequest(chatId, null, $"{category.Id}", nextUpdateId),
                CancellationToken.None);

            Assert.That(page, Is.Not.Null);
            Assert.That(page!.Buttons, Is.Not.Null);
            var recipeButtons = page.Buttons!.InlineKeyboard.SelectMany(row => row).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(page.Message, Is.EqualTo("Выбери рецепт"));
                Assert.That(RecipeButtons(recipeButtons),
                    Has.Length.EqualTo(Math.Min(5, expectedInCategory.Length)));
            });

            var displayedRecipes = RecipeButtons(recipeButtons)
                .Select(b => (b.CallbackData, b.Text)).ToList();

            var pageCount = (int)Math.Ceiling(expectedInCategory.Length / 5.0);
            for (var pageIndex = 1; pageIndex < pageCount; pageIndex++)
            {
                var nextPage = await Send(
                    new GetUserRecipesRequest(chatId, null, $"recipes:page:{pageIndex}",
                        nextUpdateId + pageIndex), CancellationToken.None);
                Assert.That(nextPage, Is.Not.Null);
                Assert.That(nextPage!.Buttons, Is.Not.Null);
                var nextButtons = RecipeButtons(nextPage.Buttons!.InlineKeyboard.SelectMany(row => row));
                Assert.That(nextButtons,
                    Has.Length.EqualTo(Math.Min(5, expectedInCategory.Length - pageIndex * 5)));
                displayedRecipes.AddRange(nextButtons.Select(b => (b.CallbackData, b.Text)));
            }

            Assert.That(displayedRecipes,
                Is.EquivalentTo(expectedInCategory.Select(r => ($"recipe:{r.Id}", $"📖 {r.Name}"))));
        }
    }

    [Test]
    public async Task GetRecipesByNameTest()
    {
        var chatId = Fixture.Create<long>();
        var otherChatId = Fixture.Create<long>();
        var updateId = Fixture.Create<int>();
        var nextUpdateId = updateId + 1;
        var expected = new List<Recipe>();

        await CreateDataForDB(chatId, otherChatId, expected);

        var sessionStore = _services!.GetRequiredService<IUserSessionStore>();
        await sessionStore.SetUserSession(chatId, new UserSession
        {
            ChatId = chatId,
            UserState = UserStates.GetMyRecipes,
            RecipeState = RecipeStates.WaitingMove
        });

        var searchPage = await Send(
            new GetUserRecipesRequest(chatId, null, "search_by_name", updateId),
            CancellationToken.None);

        Assert.That(searchPage, Is.Not.Null);
        Assert.That(searchPage!.Buttons, Is.Null);
        Assert.That(searchPage.Message, Is.EqualTo("Введите полное и частичное наименование рецепта."));

        await using (var db = await Factory.CreateDbContextAsync())
        {
            var recipe = await db.Recipes
                .Include(r => r.User)
                .FirstAsync(q=>q.User.TelegramId ==  chatId);
            
            var page = await Send(
                new GetUserRecipesRequest(chatId, recipe.Name, null, updateId),
                CancellationToken.None);
            Assert.That(page, Is.Not.Null);
            Assert.That(page!.Buttons, Is.Not.Null);
            
            var expectedInCategory = expected.Where(r => r.Name.Contains(recipe.Name) ).ToArray();
            
            var recipeButtons = page.Buttons!.InlineKeyboard.SelectMany(row => row).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(page.Message, Is.EqualTo("Выбери рецепт"));
                Assert.That(RecipeButtons(recipeButtons),
                    Has.Length.EqualTo(Math.Min(5, expectedInCategory.Length)));
            });

            var displayedRecipes = RecipeButtons(recipeButtons)
                .Select(b => (b.CallbackData, b.Text)).ToList();

            var pageCount = (int)Math.Ceiling(expectedInCategory.Length / 5.0);
            for (var pageIndex = 1; pageIndex < pageCount; pageIndex++)
            {
                var nextPage = await Send(
                    new GetUserRecipesRequest(chatId, null, $"recipes:page:{pageIndex}",
                        nextUpdateId + pageIndex), CancellationToken.None);
                Assert.That(nextPage, Is.Not.Null);
                Assert.That(nextPage!.Buttons, Is.Not.Null);
                var nextButtons = RecipeButtons(nextPage.Buttons!.InlineKeyboard.SelectMany(row => row));
                Assert.That(nextButtons,
                    Has.Length.EqualTo(Math.Min(5, expectedInCategory.Length - pageIndex * 5)));
                displayedRecipes.AddRange(nextButtons.Select(b => (b.CallbackData, b.Text)));
            }

            Assert.That(displayedRecipes,
                Is.EquivalentTo(expectedInCategory.Select(r => ($"recipe:{r.Id}", $"📖 {r.Name}"))));
        }
    }

    private async Task CreateDataForDB(long chatId, long otherChatId, List<Recipe> expected)
    {
        await using (var db = await Factory.CreateDbContextAsync())
        {
            var user = CreateUser(chatId);
            var otherUser = CreateUser(otherChatId);
            db.Users.AddRange(user, otherUser);

            var categoryCount = Random.Shared.Next(2, 11);
            var categories = new List<Category>();
            for (var i = 0; i < categoryCount; i++)
            {
                var category = new Category();
                category.SetName(Fixture.Create<string>());
                categories.Add(category);
            }

            db.Categories.AddRange(categories);
            await db.SaveChangesAsync();

            for (var i = 1; i <= 100; i++)
            {
                var category = categories[Random.Shared.Next(categories.Count)];
                expected.Add(CreateRecipe(user.Id, category.Id));
            }

            db.Recipes.AddRange(expected);
            var otherCategory = categories[Random.Shared.Next(categories.Count)];
            db.Recipes.Add(CreateRecipe(otherUser.Id, otherCategory.Id));
            await db.SaveChangesAsync();
        }
    }

    private async Task<HandlerResult?> Send(GetUserRecipesRequest request, CancellationToken cancellationToken)
    {
        using var scope = _services!.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request, cancellationToken);
    }

    private static InlineKeyboardButton[] RecipeButtons(IEnumerable<InlineKeyboardButton> buttons) =>
        buttons.Where(b => b.CallbackData?.StartsWith("recipe:") == true).ToArray();

    private User CreateUser(long chatId)
    {
        var user = new User();
        user.SetTelegramId(chatId);
        user.SetName(Fixture.Create<string>());
        return user;
    }

    private Recipe CreateRecipe(int userId, int categoryId)
    {
        var recipe = new Recipe();
        recipe.SetUserId(userId);
        recipe.SetCategoryId(categoryId);
        recipe.SetName(Fixture.Create<string>());
        recipe.SetIngredients(Fixture.Create<string>());
        return recipe;
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
}
