using AutoFixture;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeBot.Application.Abstract;
using RecipeBot.Application.Handlers;
using RecipeBot.Application.Models;
using RecipeBot.Domain.Abstract.IRepositories;
using RecipeBot.Domain.Enums;
using RecipeBot.Infrastructure;
using RecipeBot.Infrastructure.Redis;
using RecipeBot.Infrastructure.Repositories;
using RecipeBot.IntegrationTests.Repositories;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace RecipeBot.IntegrationTests;

[TestFixture]
public class AddNewRecipeIntegrationTests : RepositoryTestBase
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
                configuration.RegisterServicesFromAssemblyContaining<AddNewRecipeHandler>());
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
    public async Task AddRecipe_WithImageAndTwoStages_PersistsRecipeAndReturnsToStartMenu()
    {
        var data = await SeedOwners();
        var name = Fixture.Create<string>();
        var ingredients = Fixture.Create<string>();
        var image = Fixture.Create<string>();
        var firstDescription = Fixture.Create<string>();
        var secondDescription = Fixture.Create<string>();
        var stageImage = Fixture.Create<string>();
        var updateId = Fixture.Create<int>();
        var services = _services!;
        var sessionStore = services.GetRequiredService<IUserSessionStore>();
        await sessionStore.SetUserSession(data.Owner.TelegramId, new UserSession
        {
            ChatId = data.Owner.TelegramId,
            UserState = UserStates.AddingRecipe,
            RecipeState = RecipeStates.WaitingForRecipeName
        });

        async Task<HandlerResult?> Send(string? text = null, string? callback = null,
            string? imageFileId = null, string? caption = null)
        {
            using var scope = services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
                new AddNewRecipeRequest(data.Owner.TelegramId, text, imageFileId,
                    callback, caption, ++updateId));
        }

        var categories = await Send(text: name);
        Assert.That(categories?.Buttons, Is.Not.Null);
        Assert.That(categories!.Buttons!.InlineKeyboard.SelectMany(row => row)
            .Select(button => (button.CallbackData, button.Text)),
            Is.EquivalentTo(new[]
            {
                (data.Category.Id.ToString(), data.Category.Name),
                (data.OtherCategory.Id.ToString(), data.OtherCategory.Name)
            }));

        await Send(callback: data.Category.Id.ToString());
        await Send(text: ingredients);
        await Send(callback: "add_image");
        await Send(imageFileId: image);
        await Send(callback: "add_step");
        await Send(text: firstDescription);
        await Send(callback: "add_step");
        await Send(caption: secondDescription, imageFileId: stageImage);

        await using (var db = await Factory.CreateDbContextAsync())
        {
            Assert.That(await db.Recipes.CountAsync(), Is.Zero);
            Assert.That(await db.Stages.CountAsync(), Is.Zero);
        }

        var result = await Send(callback: "done");

        await using (var db = await Factory.CreateDbContextAsync())
        {
            var saved = await db.Recipes.AsNoTracking().Include(recipe => recipe.Stages).SingleAsync();
            Assert.Multiple(() =>
            {
                Assert.That(saved.UserId, Is.EqualTo(data.Owner.Id));
                Assert.That(saved.CategoryId, Is.EqualTo(data.Category.Id));
                Assert.That(saved.Name, Is.EqualTo(name));
                Assert.That(saved.Ingredients, Is.EqualTo(ingredients));
                Assert.That(saved.UrlImage, Is.EqualTo(image));
                Assert.That(saved.Stages.Select(stage => (stage.Number, stage.Description, stage.UrlImage)),
                    Is.EquivalentTo(new[]
                    {
                        (1, firstDescription, (string?)null),
                        (2, secondDescription, (string?)stageImage)
                    }));
            });
        }

        using var reader = await ConnectionMultiplexer.ConnectAsync(_redis!.GetConnectionString());
        var session = await new UserSessionsStore(new RedisService(reader))
            .TryGetSession(data.Owner.TelegramId);
        Assert.That(session, Is.Not.Null);
        Assert.That(session!.UserState, Is.EqualTo(UserStates.Start));
        Assert.That(result?.Buttons, Is.Not.Null);
        Assert.That(result!.Buttons!.InlineKeyboard.SelectMany(row => row)
            .Select(button => button.CallbackData),
            Is.EquivalentTo(new[] { "add_recipe", "my_recipe" }));
    }
}
