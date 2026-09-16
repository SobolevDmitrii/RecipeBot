using AutoFixture;
using Moq;
using RecipeBot.Application.Abstract;
using RecipeBot.Application.Handlers;
using RecipeBot.Application.Models;
using RecipeBot.Domain.Abstract.IRepositories;
using RecipeBot.Domain.Enums;
using RecipeBot.Domain.Models;

namespace RecipeBot.UnitTests;

[TestFixture]
public class GetRecipeTests
{
    private Fixture _fixture = null!;

    private Mock<IRecipeRepository> _recipeRepositoryMock = null!;
    private Mock<ICategoryRepository> _categoryRepositoryMock = null!;
    private Mock<IUserSessionStore> _userSessionStoreMock = null!;

    private GetUserRecipesHandler _handler = null!;
    private UserSession _userSession = null!;

    private long _chatId;
    private int _updateId;

    [SetUp]
    public void SetUp()
    {
        _fixture = new Fixture();

        _recipeRepositoryMock = new Mock<IRecipeRepository>();
        _categoryRepositoryMock = new Mock<ICategoryRepository>();
        _userSessionStoreMock = new Mock<IUserSessionStore>();

        _chatId = _fixture.Create<long>();
        _updateId = _fixture.Create<int>();

        _userSession = new UserSession();

        _userSessionStoreMock
            .Setup(x => x.GetOrAddUserSession(_chatId))
            .ReturnsAsync(_userSession);

        _handler = new GetUserRecipesHandler(
            _recipeRepositoryMock.Object,
            _categoryRepositoryMock.Object,
            _userSessionStoreMock.Object);
    }

    [Test]
    public async Task ShowRecipeSelectionOptions()
    {
        // Arrange
        _userSession.RecipeState =
            RecipeStates.ShowRecipeSelectionOptions;

        var request = CreateRequest();

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);

            Assert.That(
                result!.Message,
                Is.EqualTo("✅ Выберите один из вариантов."));

            Assert.That(
                result.Buttons,
                Is.Not.Null);

            Assert.That(
                _userSession.RecipeState,
                Is.EqualTo(RecipeStates.WaitingMove));
        });
    }

    [Test]
    public async Task AllListCallback()
    {
        // Arrange
        var recipes = CreateRecipes(3);

        _userSession.RecipeState =
            RecipeStates.WaitingMove;

        _recipeRepositoryMock
            .Setup(x => x.GetListByTelegramId(
                _chatId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(recipes);

        var request = CreateRequest(
            callbackData: "all_list");

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);

            Assert.That(
                result!.Message,
                Is.EqualTo("Выбери рецепт"));

            Assert.That(
                result.Buttons,
                Is.Not.Null);

            Assert.That(
                _userSession.RecipeState,
                Is.EqualTo(RecipeStates.ShowRecipces));

            Assert.That(
                _userSession.Recipes,
                Has.Count.EqualTo(3));
        });

        _recipeRepositoryMock.Verify(
            x => x.GetListByTelegramId(
                _chatId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task ForCategoryCallback()
    {
        // Arrange
        var categoryName = _fixture.Create<string>();

        var category = new Category();
        category.SetName(categoryName);

        _userSession.RecipeState =
            RecipeStates.WaitingMove;

        _categoryRepositoryMock
            .Setup(x => x.GetAllCategories(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([category]);

        var request = CreateRequest(
            callbackData: "for_category");

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);

            Assert.That(
                result!.Message,
                Is.EqualTo("✅ Выберите категорию."));

            Assert.That(
                result.Buttons,
                Is.Not.Null);

            Assert.That(
                _userSession.RecipeState,
                Is.EqualTo(RecipeStates.WaitingForCategory));
        });

        _categoryRepositoryMock.Verify(
            x => x.GetAllCategories(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task WaitingForCategory()
    {
        // Arrange
        var categoryId = _fixture.Create<int>();

        if (categoryId < 0)
            categoryId *= -1;

        categoryId++;

        var recipes = CreateRecipes(2);

        _userSession.RecipeState =
            RecipeStates.WaitingForCategory;

        _recipeRepositoryMock
            .Setup(x => x.GetListByTelegramIdAndCategory(
                _chatId,
                categoryId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(recipes);

        var request = CreateRequest(
            callbackData: categoryId.ToString());

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);

            Assert.That(
                result!.Message,
                Is.EqualTo("Выбери рецепт"));

            Assert.That(
                _userSession.RecipeState,
                Is.EqualTo(RecipeStates.ShowRecipces));

            Assert.That(
                _userSession.Recipes,
                Has.Count.EqualTo(2));
        });

        _recipeRepositoryMock.Verify(
            x => x.GetListByTelegramIdAndCategory(
                _chatId,
                categoryId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task SearchByNameCallback()
    {
        // Arrange
        _userSession.RecipeState =
            RecipeStates.WaitingMove;

        var request = CreateRequest(
            callbackData: "search_by_name");

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);

            Assert.That(
                result!.Message,
                Is.EqualTo(
                    "Введите полное и частичное наименование рецепта."));

            Assert.That(
                _userSession.RecipeState,
                Is.EqualTo(RecipeStates.WaitingForRecipeName));
        });
    }

    [Test]
    public async Task WaitingForRecipeName()
    {
        // Arrange
        var searchText = _fixture.Create<string>();
        var recipes = CreateRecipes(2);

        _userSession.RecipeState =
            RecipeStates.WaitingForRecipeName;

        _recipeRepositoryMock
            .Setup(x => x.SearchByName(
                _chatId,
                searchText,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(recipes);

        var request = CreateRequest(
            text: searchText);

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);

            Assert.That(
                result!.Message,
                Is.EqualTo("Выбери рецепт"));

            Assert.That(
                _userSession.RecipeState,
                Is.EqualTo(RecipeStates.ShowRecipces));

            Assert.That(
                _userSession.Recipes,
                Has.Count.EqualTo(2));
        });

        _recipeRepositoryMock.Verify(
            x => x.SearchByName(
                _chatId,
                searchText,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task ShowRecipes_PageCallback()
    {
        // Arrange
        _userSession.RecipeState =
            RecipeStates.ShowRecipces;

        _userSession.Recipes =
            Enumerable
                .Range(1, 8)
                .Select(x =>
                    _fixture
                        .Build<RecipeDto>()
                        .With(r => r.Id, x)
                        .With(r => r.Name, _fixture.Create<string>())
                        .Create())
                .ToList();

        var request = CreateRequest(
            callbackData: "recipes:page:1");

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);

            Assert.That(
                result!.Message,
                Is.EqualTo("📋 Ваши рецепты:"));

            Assert.That(
                result.Buttons,
                Is.Not.Null);
        });

        _recipeRepositoryMock.Verify(
            x => x.GetListByTelegramId(
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task ShowRecipes_SelectRecipe()
    {
        // Arrange
        var recipeId = CreatePositiveInt();

        _userSession.RecipeState =
            RecipeStates.ShowRecipces;

        var request = CreateRequest(
            callbackData: $"recipe:{recipeId}");

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);

            Assert.That(
                result!.Message,
                Is.EqualTo("Выбери действие"));

            Assert.That(
                result.Buttons,
                Is.Not.Null);

            Assert.That(
                _userSession.RecipeState,
                Is.EqualTo(RecipeStates.WaitMoveForRecipe));
        });
    }

    [Test]
    public async Task ShowRecipes_InvalidRecipeCallback()
    {
        // Arrange
        _userSession.RecipeState =
            RecipeStates.ShowRecipces;

        var request = CreateRequest(
            callbackData: "recipe:not-number");

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ViewRecipe()
    {
        // Arrange
        var recipeId = CreatePositiveInt();

        var recipeName = _fixture.Create<string>();
        var ingredients = _fixture.Create<string>();
        var categoryName = _fixture.Create<string>();
        var imageId = _fixture.Create<string>();

        var recipe = CreateRecipe(
            recipeName,
            ingredients,
            categoryName,
            imageId);

        SetPrivateProperty(
            recipe,
            nameof(Recipe.Id),
            recipeId);

        _userSession.RecipeState =
            RecipeStates.WaitMoveForRecipe;

        _recipeRepositoryMock
            .Setup(x => x.Get(
                _chatId,
                recipeId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(recipe);

        var request = CreateRequest(
            callbackData: $"recipe:view:{recipeId}");

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);

            Assert.That(
                result!.Message,
                Does.Contain(recipeName));

            Assert.That(
                result.Message,
                Does.Contain(categoryName));

            Assert.That(
                result.Message,
                Does.Contain(ingredients));

            Assert.That(
                result.UrlImage,
                Is.EqualTo(imageId));

            Assert.That(
                result.Buttons,
                Is.Not.Null);

            Assert.That(
                _userSession.CurrentViewRecipe,
                Is.Not.Null);

            Assert.That(
                _userSession.CurrentViewRecipe!.Name,
                Is.EqualTo(recipeName));

            Assert.That(
                _userSession.CurrentViewRecipe.Id,
                Is.EqualTo(recipeId));
        });

        _recipeRepositoryMock.Verify(
            x => x.Get(
                _chatId,
                recipeId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task DeleteRecipe()
    {
        // Arrange
        var recipeId = CreatePositiveInt();

        _userSession.RecipeState =
            RecipeStates.WaitMoveForRecipe;

        var request = CreateRequest(
            callbackData: $"recipe:delete:{recipeId}");

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);

            Assert.That(
                result!.Message,
                Is.EqualTo("✅ Выберите один из вариантов."));

            Assert.That(
                _userSession.RecipeState,
                Is.EqualTo(RecipeStates.WaitingMove));
        });

        _recipeRepositoryMock.Verify(
            x => x.Delete(
                _chatId,
                recipeId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task RecipeBack()
    {
        // Arrange
        _userSession.RecipeState =
            RecipeStates.WaitMoveForRecipe;

        var request = CreateRequest(
            callbackData: "recipe:back");

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);

            Assert.That(
                result!.Message,
                Is.EqualTo("✅ Выберите один из вариантов."));

            Assert.That(
                _userSession.RecipeState,
                Is.EqualTo(RecipeStates.WaitingMove));
        });
    }

    [Test]
    public async Task ViewStage()
    {
        // Arrange
        var description = _fixture.Create<string>();

        _userSession.RecipeState =
            RecipeStates.WaitMoveForRecipe;

        _userSession.CurrentViewRecipe =
            _fixture
                .Build<RecipeDto>()
                .With(x => x.Stages,
                [
                    _fixture
                        .Build<StageDto>()
                        .With(x => x.Number, 1)
                        .With(x => x.Description, description)
                        .Create()
                ])
                .Create();

        var request = CreateRequest(
            callbackData: "recipe:stages:0");

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);

        Assert.That(
            result!.Message,
            Does.Contain(description));
    }
    

    [Test]
    public async Task CancelCallback()
    {
        // Arrange
        _userSession.RecipeState =
            RecipeStates.WaitingMove;

        var request = CreateRequest(
            callbackData: "cancel");

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);

            Assert.That(
                _userSession.UserState,
                Is.EqualTo(UserStates.Cansel));
        });

        _userSessionStoreMock.Verify(
            x => x.SetUserSession(
                _chatId,
                It.Is<UserSession>(session =>
                    session.UserState ==
                    UserStates.Cansel)),
            Times.AtLeastOnce);
    }

    #region Helpers

    private GetUserRecipesRequest CreateRequest(
        string? text = null,
        string? callbackData = null)
    {
        return new GetUserRecipesRequest(
            ChatId: _chatId,
            text: text,
            CallbackData: callbackData,
            UpdateId: _updateId);
    }

    private List<Recipe> CreateRecipes(int count)
    {
        return Enumerable
            .Range(0, count)
            .Select(_ =>
                CreateRecipe(
                    _fixture.Create<string>(),
                    _fixture.Create<string>(),
                    _fixture.Create<string>()))
            .ToList();
    }

    private Recipe CreateRecipe(
        string recipeName,
        string ingredients,
        string categoryName,
        string? imageId = null)
    {
        var recipe = new Recipe();

        recipe.SetName(recipeName);
        recipe.SetIngredients(ingredients);
        recipe.SetCategoryId(CreatePositiveInt());

        if (imageId != null)
        {
            recipe.SetUrlImage(imageId);
        }

        var stage = new Stage();

        stage.SetNumber(1);
        stage.SetDescription(
            _fixture.Create<string>());

        recipe.AddStage(stage);

        var category = new Category();
        category.SetName(categoryName);

        SetPrivateProperty(
            recipe,
            nameof(Recipe.Category),
            category);

        return recipe;
    }

    private int CreatePositiveInt()
    {
        return _fixture.Create<int>() switch
        {
            int.MinValue => 1,
            var value => Math.Abs(value) + 1
        };
    }

    private static void SetPrivateProperty<T>(
        object target,
        string propertyName,
        T value)
    {
        var property = target
            .GetType()
            .GetProperty(propertyName);

        property!.SetValue(
            target,
            value);
    }

    #endregion
}