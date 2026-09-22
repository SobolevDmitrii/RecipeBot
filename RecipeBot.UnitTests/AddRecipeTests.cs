using AutoFixture;
using MediatR;
using Moq;
using RecipeBot.Application.Abstract;
using RecipeBot.Application.Handlers;
using RecipeBot.Application.Models;
using RecipeBot.Domain.Abstract.IRepositories;
using RecipeBot.Domain.Enums;
using RecipeBot.Domain.Models;

namespace RecipeBot.UnitTests;

[TestFixture]
public class AddRecipeTests
{
    private Fixture _fixture = null!;

    private Mock<IMediator> _mediatorMock = null!;
    private Mock<IRecipeRepository> _recipeRepositoryMock = null!;
    private Mock<ICategoryRepository> _categoryRepositoryMock = null!;
    private Mock<IUserSessionStore> _userSessionStoreMock = null!;

    private AddNewRecipeHandler _handler = null!;
    private UserSession _userSession = null!;

    private long _chatId;
    private int _updateId;

    [SetUp]
    public void SetUp()
    {
        _fixture = new Fixture();

        _mediatorMock = new Mock<IMediator>();
        _recipeRepositoryMock = new Mock<IRecipeRepository>();
        _categoryRepositoryMock = new Mock<ICategoryRepository>();
        _userSessionStoreMock = new Mock<IUserSessionStore>();

        _chatId = _fixture.Create<long>();
        _updateId = _fixture.Create<int>();

        _userSession = new UserSession
        {
            RecipeDraft = new RecipeDto()
        };

        _userSessionStoreMock
            .Setup(x => x.TryGetSession(_chatId))
            .ReturnsAsync(_userSession);

        _handler = new AddNewRecipeHandler(
            _mediatorMock.Object,
            _recipeRepositoryMock.Object,
            _categoryRepositoryMock.Object,
            _userSessionStoreMock.Object);
    }

    [Test]
    public async Task NegativeTest_SessionIsNullReturnError()
    {
        // Arrange
        var text = _fixture.Create<string>();

        _userSessionStoreMock
            .Setup(x => x.TryGetSession(_chatId))
            .ReturnsAsync((UserSession?)null);

        var request = CreateRequest(text: text);

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);

        Assert.That(
            result!.Message,
            Is.EqualTo("Произошла непредвиденная ошибка, повторите снова"));

        _userSessionStoreMock.Verify(
            x => x.AddProcessing(It.IsAny<long>()),
            Times.Never);

        _userSessionStoreMock.Verify(
            x => x.SetUserSession(
                It.IsAny<long>(),
                It.IsAny<UserSession>()),
            Times.Never);
    }

    [Test]
    public async Task PositiveTest_SessionSetChatIdAndUpdateId()
    {
        // Arrange
        _userSession.RecipeState = RecipeStates.WaitingMove;

        var request = CreateRequest();

        // Act
        await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_userSession.ChatId, Is.EqualTo(_chatId));
            Assert.That(_userSession.CurrentUpdateId, Is.EqualTo(_updateId));
        });

        _userSessionStoreMock.Verify(
            x => x.AddProcessing(_chatId),
            Times.Once);

        _userSessionStoreMock.Verify(
            x => x.SetUserSession(
                _chatId,
                _userSession),
            Times.Once);
    }

    [Test]
    public async Task WaitingForRecipeName()
    {
        // Arrange
        var recipeName = _fixture.Create<string>();

        _userSession.RecipeState =
            RecipeStates.WaitingForRecipeName;

        _categoryRepositoryMock
            .Setup(x => x.GetAllCategories(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var request = CreateRequest(
            text: recipeName);

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
                _userSession.RecipeDraft,
                Is.Not.Null);

            Assert.That(
                _userSession.RecipeDraft!.Name,
                Is.EqualTo(recipeName));

            Assert.That(
                _userSession.RecipeState,
                Is.EqualTo(RecipeStates.WaitingForCategory));
        });

        _categoryRepositoryMock.Verify(
            x => x.GetAllCategories(
                It.IsAny<CancellationToken>()),
            Times.Once);

        _userSessionStoreMock.Verify(
            x => x.SetUserSession(
                _chatId,
                It.Is<UserSession>(session =>
                    session.RecipeDraft != null &&
                    session.RecipeDraft.Name == recipeName &&
                    session.RecipeState ==
                    RecipeStates.WaitingForCategory)),
            Times.AtLeastOnce);

        _recipeRepositoryMock.Verify(
            x => x.Add(
                It.IsAny<Recipe>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task WaitingForCategory()
    {
        // Arrange
        var categoryId = _fixture.Create<int>();

        _userSession.RecipeState =
            RecipeStates.WaitingForCategory;

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
                Is.EqualTo("✏️ Введите ингредиенты:"));

            Assert.That(
                _userSession.RecipeDraft!.CategoryId,
                Is.EqualTo(categoryId));

            Assert.That(
                _userSession.RecipeState,
                Is.EqualTo(RecipeStates.WaitingForIngredients));
        });
    }

    [Test]
    public async Task WaitingForIngredients()
    {
        // Arrange
        var ingredients = _fixture.Create<string>();

        _userSession.RecipeState =
            RecipeStates.WaitingForIngredients;

        var request = CreateRequest(
            text: ingredients);

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
                _userSession.RecipeDraft!.Ingredients,
                Is.EqualTo(ingredients));

            Assert.That(
                _userSession.RecipeState,
                Is.EqualTo(RecipeStates.WaitingMove));
        });
    }

    [Test]
    public async Task AddImageCallback()
    {
        // Arrange
        _userSession.RecipeState =
            RecipeStates.WaitingMove;

        var request = CreateRequest(
            callbackData: "add_image");

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
                Is.EqualTo("Загрузите и отправте картинку в чат."));

            Assert.That(
                _userSession.RecipeState,
                Is.EqualTo(RecipeStates.WaitingForImageRecipe));
        });
    }

    [Test]
    public async Task WaitingForImageRecipe()
    {
        // Arrange
        var imageFileId = _fixture.Create<string>();

        _userSession.RecipeState =
            RecipeStates.WaitingForImageRecipe;

        var request = CreateRequest(
            imageFileId: imageFileId);

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
                _userSession.RecipeDraft!.UrlImage,
                Is.EqualTo(imageFileId));

            Assert.That(
                _userSession.RecipeState,
                Is.EqualTo(RecipeStates.WaitingMove));
        });
    }

    [Test]
    public async Task AddStepCallback()
    {
        // Arrange
        _userSession.RecipeState =
            RecipeStates.WaitingMove;

        var request = CreateRequest(
            callbackData: "add_step");

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
                    "✏️ Введите описание этапа, если хотите добавить картинку этапа, просто пришлите ее вместе с текстом:"));

            Assert.That(
                _userSession.RecipeState,
                Is.EqualTo(RecipeStates.AddingStage));
        });
    }

    [Test]
    public async Task AddingFirstStage()
    {
        // Arrange
        var description = _fixture.Create<string>();

        _userSession.RecipeState =
            RecipeStates.AddingStage;

        var request = CreateRequest(
            text: description);

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
                _userSession.RecipeDraft!.Stages,
                Has.Count.EqualTo(1));

            Assert.That(
                _userSession.RecipeDraft.Stages[0].Number,
                Is.EqualTo(1));

            Assert.That(
                _userSession.RecipeDraft.Stages[0].Description,
                Is.EqualTo(description));

            Assert.That(
                _userSession.RecipeDraft.Stages[0].UrlImage,
                Is.Null);
        });
    }

    [Test]
    public async Task AddingNextStage()
    {
        // Arrange
        var firstDescription = _fixture.Create<string>();
        var secondDescription = _fixture.Create<string>();

        _userSession.RecipeState =
            RecipeStates.AddingStage;

        var firstStage = _fixture
            .Build<StageDto>()
            .With(x => x.Number, 1)
            .With(x => x.Description, firstDescription)
            .Without(x => x.UrlImage)
            .Create();

        _userSession.RecipeDraft!.Stages.Add(firstStage);

        var request = CreateRequest(
            text: secondDescription);

        // Act
        await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(
                _userSession.RecipeDraft.Stages,
                Has.Count.EqualTo(2));

            Assert.That(
                _userSession.RecipeDraft.Stages[1].Number,
                Is.EqualTo(2));

            Assert.That(
                _userSession.RecipeDraft.Stages[1].Description,
                Is.EqualTo(secondDescription));
        });
    }

    [Test]
    public async Task StageContainsImageAndCaption()
    {
        // Arrange
        var caption = _fixture.Create<string>();
        var imageFileId = _fixture.Create<string>();

        _userSession.RecipeState =
            RecipeStates.AddingStage;

        var request = CreateRequest(
            caption: caption,
            imageFileId: imageFileId);

        // Act
        await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        var stage =
            _userSession.RecipeDraft!.Stages.Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                stage.Number,
                Is.EqualTo(1));

            Assert.That(
                stage.Description,
                Is.EqualTo(caption));

            Assert.That(
                stage.UrlImage,
                Is.EqualTo(imageFileId));
        });
    }

    [Test]
    public async Task CancelCallback()
    {
        // Arrange
        _userSession.UserState =
            UserStates.AddingRecipe;

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

        _recipeRepositoryMock.Verify(
            x => x.Add(
                It.IsAny<Recipe>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Done()
    {
        // Arrange
        var expectedResult = new HandlerResult(
            _fixture.Create<string>(),
            null,
            null);

        var recipeName = _fixture.Create<string>();
        var ingredients = _fixture.Create<string>();
        var stageDescription = _fixture.Create<string>();
        var categoryId = _fixture.Create<int>();

        _userSession.RecipeState =
            RecipeStates.WaitingMove;

        _userSession.RecipeDraft =
            CreateCompletedRecipeDraft(
                recipeName,
                categoryId,
                ingredients,
                stageDescription);

        _recipeRepositoryMock
            .Setup(x => x.Add(
                It.IsAny<Recipe>(),
                _chatId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Recipe());

        _mediatorMock
            .Setup(x => x.Send(
                It.IsAny<StartMenuRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var request = CreateRequest(
            callbackData: "done");

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.That(
            result,
            Is.SameAs(expectedResult));

        _recipeRepositoryMock.Verify(
            x => x.Add(
                It.Is<Recipe>(recipe =>
                    recipe.Name == recipeName &&
                    recipe.CategoryId == categoryId &&
                    recipe.Ingredients == ingredients &&
                    recipe.Stages.Count == 1 &&
                    recipe.Stages[0].Description ==
                    stageDescription),
                _chatId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            x => x.Send(
                It.Is<StartMenuRequest>(menu =>
                    menu.ChatId == _chatId &&
                    menu.CallBack == "done"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task RequestContainsNoData()
    {
        // Arrange
        _userSession.RecipeState =
            RecipeStates.WaitingMove;

        var request = CreateRequest();

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);

        _recipeRepositoryMock.Verify(
            x => x.Add(
                It.IsAny<Recipe>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _mediatorMock.Verify(
            x => x.Send(
                It.IsAny<StartMenuRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public void TextReceivedInUnsupportedRecipeState()
    {
        // Arrange
        _userSession.RecipeState =
            RecipeStates.WaitingMove;

        var request = CreateRequest(
            text: _fixture.Create<string>());

        // Act + Assert
        Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await _handler.Handle(
                    request,
                    CancellationToken.None));
    }

    #region Helpers

    private AddNewRecipeRequest CreateRequest(
        string? text = null,
        string? imageFileId = null,
        string? callbackData = null,
        string? caption = null)
    {
        return new AddNewRecipeRequest(
            ChatId: _chatId,
            Text: text,
            ImageFileId: imageFileId,
            CallbackData: callbackData,
            Caption: caption,
            UpdateId: _updateId);
    }

    private RecipeDto CreateCompletedRecipeDraft(
        string recipeName,
        int categoryId,
        string ingredients,
        string stageDescription)
    {
        var stage = _fixture
            .Build<StageDto>()
            .With(x => x.Number, 1)
            .With(x => x.Description, stageDescription)
            .Without(x => x.UrlImage)
            .Create();

        return _fixture
            .Build<RecipeDto>()
            .With(x => x.Name, recipeName)
            .With(x => x.CategoryId, categoryId)
            .With(x => x.Ingredients, ingredients)
            .With(x => x.Stages, [stage])
            .Create();
    }

    #endregion
}