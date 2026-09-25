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
public class EditRecipeTests
{
    private Fixture _fixture = null!;
    private Mock<IRecipeRepository> _recipeRepositoryMock = null!;
    private Mock<ICategoryRepository> _categoryRepositoryMock = null!;
    private Mock<IUserSessionStore> _userSessionStoreMock = null!;
    private EditRecipeHandler _handler = null!;
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
        _userSession = new UserSession
        {
            UserState = UserStates.EditRecipe,
            RecipeState = RecipeStates.WaitingMove,
            RecipeDraft = _fixture.Build<RecipeDto>()
                .With(x => x.Stages, new List<StageDto> { CreateStage(1), CreateStage(2) })
                .Create()
        };
        _userSessionStoreMock
            .Setup(x => x.TryGetSession(_chatId))
            .ReturnsAsync(_userSession);
        _handler = new EditRecipeHandler(
            _recipeRepositoryMock.Object,
            _categoryRepositoryMock.Object,
            _userSessionStoreMock.Object);
    }

    [Test]
    public async Task SessionExpired()
    {
        // Arrange
        _userSessionStoreMock.Setup(x => x.TryGetSession(_chatId))
            .ReturnsAsync((UserSession?)null);
        var request = CreateRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.That(result!.Message,
            Is.EqualTo("Сессия истекла. Отправьте /start и выберите рецепт снова."));
        _userSessionStoreMock.Verify(x => x.AddProcessing(It.IsAny<long>()), Times.Never);
        _userSessionStoreMock.Verify(
            x => x.SetUserSession(It.IsAny<long>(), It.IsAny<UserSession>()), Times.Never);
        _recipeRepositoryMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task RequestContainsNoData()
    {
        // Arrange
        var request = CreateRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(_userSession.ChatId, Is.EqualTo(_chatId));
            Assert.That(_userSession.CurrentUpdateId, Is.EqualTo(_updateId));
            Assert.That(_userSession.RecipeState, Is.EqualTo(RecipeStates.WaitingMove));
        });
        _userSessionStoreMock.Verify(x => x.AddProcessing(_chatId), Times.Once);
        _userSessionStoreMock.Verify(x => x.SetUserSession(_chatId, _userSession), Times.Exactly(2));
        _recipeRepositoryMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task GetRecipeAndMenu()
    {
        // Arrange
        _userSession.RecipeState = RecipeStates.GetMenuEdit;
        var recipeId = _fixture.Create<int>();
        var recipe = new Recipe();
        recipe.SetName(_fixture.Create<string>());
        recipe.SetIngredients(_fixture.Create<string>());
        recipe.SetCategoryId(_fixture.Create<int>());
        recipe.SetUrlImage(_fixture.Create<string>());
        using var cancellation = new CancellationTokenSource();
        _recipeRepositoryMock.Setup(x => x.Get(_chatId, recipeId, cancellation.Token))
            .ReturnsAsync(recipe);
        var request = CreateRequest(callbackData: $"recipe:edit:{recipeId}");

        // Act
        var result = await _handler.Handle(request, cancellation.Token);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result!.Message,
                Is.EqualTo($"✏️ Редактирование рецепта «{recipe.Name}». Выберите поле."));
            Assert.That(_userSession.RecipeDraft!.Name, Is.EqualTo(recipe.Name));
            Assert.That(_userSession.RecipeDraft.Ingredients, Is.EqualTo(recipe.Ingredients));
            Assert.That(_userSession.RecipeDraft.CategoryId, Is.EqualTo(recipe.CategoryId));
            Assert.That(_userSession.RecipeDraft.UrlImage, Is.EqualTo(recipe.UrlImage));
            Assert.That(_userSession.RecipeState, Is.EqualTo(RecipeStates.WaitingMove));
            Assert.That(GetCallbacks(result), Does.Contain("edit:save"));
        });
        _recipeRepositoryMock.Verify(x => x.Get(_chatId, recipeId, cancellation.Token), Times.Once);
    }

    [Test]
    public async Task RecipeNotFound()
    {
        // Arrange
        _userSession.RecipeState = RecipeStates.GetMenuEdit;
        _recipeRepositoryMock.Setup(x => x.Get(_chatId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());
        var request = CreateRequest(callbackData: "recipe:edit:1");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result!.Message, Is.EqualTo("Рецепт не найден. Выберите его снова."));
            Assert.That(GetCallbacks(result), Is.EqualTo(new[] { "my_recipe" }));
        });
    }

    
    public async Task CancelCallback()
    {
        // Arrange
        var request = CreateRequest(callbackData: "edit:cancel");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result!.Message, Is.EqualTo("Редактирование отменено. Рецепт не изменён."));
            Assert.That(_userSession.UserState, Is.EqualTo(UserStates.GetMyRecipes));
            Assert.That(_userSession.RecipeState, Is.EqualTo(RecipeStates.ShowRecipeSelectionOptions));
            Assert.That(GetCallbacks(result), Is.EqualTo(new[] { "my_recipe" }));
        });
        _recipeRepositoryMock.VerifyNoOtherCalls();
    }

    [TestCase("edit:name", RecipeStates.WaitingForRecipeName)]
    [TestCase("edit:ingredients", RecipeStates.WaitingForIngredients)]
    [TestCase("edit:image", RecipeStates.WaitingForImageRecipe)]
    public async Task SelectRecipeField(string callbackData, RecipeStates expectedState)
    {
        // Arrange
        var request = CreateRequest(callbackData: callbackData);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(_userSession.RecipeState, Is.EqualTo(expectedState));
            Assert.That(GetCallbacks(result!), Is.EqualTo(new[] { "edit:back", "edit:cancel" }));
            Assert.That(result!.UrlImage,
                Is.EqualTo(callbackData == "edit:image" ? _userSession.RecipeDraft!.UrlImage : null));
            if (callbackData == "edit:ingredients")
                Assert.That(result.Message, Does.Contain(_userSession.RecipeDraft!.Ingredients));
        });
    }

    [TestCase(RecipeStates.WaitingForRecipeName)]
    [TestCase(RecipeStates.WaitingForIngredients)]
    [TestCase(RecipeStates.WaitingForImageRecipe)]
    public async Task UpdateRecipeField(RecipeStates state)
    {
        // Arrange
        _userSession.RecipeState = state;
        var value = _fixture.Create<string>();
        var request = state == RecipeStates.WaitingForImageRecipe
            ? CreateRequest(imageFileId: value)
            : CreateRequest(text: value);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var actualValue = state switch
        {
            RecipeStates.WaitingForRecipeName => _userSession.RecipeDraft!.Name,
            RecipeStates.WaitingForIngredients => _userSession.RecipeDraft!.Ingredients,
            _ => _userSession.RecipeDraft!.UrlImage
        };
        Assert.Multiple(() =>
        {
            Assert.That(actualValue, Is.EqualTo(value));
            Assert.That(_userSession.RecipeState, Is.EqualTo(RecipeStates.WaitingMove));
            Assert.That(result!.Message,
                Is.EqualTo("✅ Черновик обновлён. Чтобы применить изменения, нажмите «Сохранить»."));
            Assert.That(GetCallbacks(result), Does.Contain("edit:save"));
        });
        _recipeRepositoryMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task RemoveRecipeImage()
    {
        // Arrange
        var request = CreateRequest(callbackData: "edit:remove_image");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_userSession.RecipeDraft!.UrlImage, Is.Null);
            Assert.That(_userSession.RecipeState, Is.EqualTo(RecipeStates.WaitingMove));
            Assert.That(result!.Message, Is.EqualTo("🖼️ Фотография удалена."));
        });
        _recipeRepositoryMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task SelectCategory()
    {
        // Arrange
        var category = CreateCategory();
        using var cancellation = new CancellationTokenSource();
        _categoryRepositoryMock.Setup(x => x.GetAllCategories(cancellation.Token))
            .ReturnsAsync([category]);
        var request = CreateRequest(callbackData: "edit:category");

        // Act
        var result = await _handler.Handle(request, cancellation.Token);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result!.Message, Is.EqualTo("✅ Выберите категорию."));
            Assert.That(_userSession.RecipeState, Is.EqualTo(RecipeStates.WaitingForCategory));
            Assert.That(GetCallbacks(result), Is.EqualTo(new[] { category.Id.ToString() }));
            Assert.That(result.Buttons!.InlineKeyboard.Single().Single().Text, Is.EqualTo(category.Name));
        });
        _categoryRepositoryMock.Verify(x => x.GetAllCategories(cancellation.Token), Times.Once);
    }

    [Test]
    public async Task UpdateCategory()
    {
        // Arrange
        _userSession.RecipeState = RecipeStates.WaitingForCategory;
        var categoryId = _fixture.Create<int>();
        var request = CreateRequest(callbackData: categoryId.ToString());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_userSession.RecipeDraft!.CategoryId, Is.EqualTo(categoryId));
            Assert.That(_userSession.RecipeState, Is.EqualTo(RecipeStates.WaitingMove));
            Assert.That(GetCallbacks(result!), Does.Contain("edit:save"));
        });
        _recipeRepositoryMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task GetStages()
    {
        // Arrange
        var request = CreateRequest(callbackData: "edit:stages");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result!.Message, Is.EqualTo("✅ Выберите этап."));
            Assert.That(_userSession.RecipeState, Is.EqualTo(RecipeStates.WaitingForSelectStage));
            Assert.That(GetCallbacks(result), Is.EqualTo(new[]
                { "edit:stage:1", "edit:stage:2", "edit:stages:add", "edit:stages:back" }));
        });
    }

    [Test]
    public async Task SelectStage()
    {
        // Arrange
        _userSession.RecipeState = RecipeStates.WaitingForSelectStage;
        var stage = _userSession.RecipeDraft!.Stages[1];
        var request = CreateRequest(callbackData: "edit:stage:2");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_userSession.CurrentViewStage, Is.SameAs(stage));
            Assert.That(_userSession.RecipeState, Is.EqualTo(RecipeStates.WaitingMoveForEditStage));
            Assert.That(result!.Message, Does.Contain(stage.Description));
            Assert.That(result.UrlImage, Is.EqualTo(stage.UrlImage));
            Assert.That(GetCallbacks(result), Is.EqualTo(new[]
            {
                "edit:stage:photo", "edit:stage:delete:photo", "edit:stage:text",
                "edit:stage:delete", "edit:stage:save", "edit:stage:back"
            }));
        });
    }

    [TestCase("edit:stage:text", RecipeStates.WaitingDescription, "Пришлите новое описание этапа")]
    [TestCase("edit:stage:photo", RecipeStates.WaitingImageStage, "Отправьте новое фото")]
    public async Task SelectStageField(string callbackData, RecipeStates state, string message)
    {
        // Arrange
        _userSession.RecipeState = RecipeStates.WaitingMoveForEditStage;
        _userSession.CurrentViewStage = _userSession.RecipeDraft!.Stages[0];
        var request = CreateRequest(callbackData: callbackData);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_userSession.RecipeState, Is.EqualTo(state));
            Assert.That(result!.Message, Is.EqualTo(message));
            Assert.That(GetCallbacks(result), Is.EqualTo(new[] { "edit:stage:move:back", "edit:cancel" }));
        });
    }

    [TestCase(RecipeStates.WaitingDescription)]
    [TestCase(RecipeStates.WaitingImageStage)]
    public async Task UpdateStageField(RecipeStates state)
    {
        // Arrange
        _userSession.RecipeState = state;
        _userSession.CurrentViewStage = _userSession.RecipeDraft!.Stages[0];
        var value = _fixture.Create<string>();
        var request = state == RecipeStates.WaitingDescription
            ? CreateRequest(text: value)
            : CreateRequest(imageFileId: value);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(state == RecipeStates.WaitingDescription
                ? _userSession.CurrentViewStage.Description : _userSession.CurrentViewStage.UrlImage,
                Is.EqualTo(value));
            Assert.That(_userSession.RecipeState, Is.EqualTo(RecipeStates.WaitingMoveForEditStage));
            Assert.That(result!.Message, Does.Contain(_userSession.CurrentViewStage.Description));
            Assert.That(result.UrlImage, Is.EqualTo(_userSession.CurrentViewStage.UrlImage));
        });
        _recipeRepositoryMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task RemoveStageImage()
    {
        // Arrange
        _userSession.RecipeState = RecipeStates.WaitingMoveForEditStage;
        _userSession.CurrentViewStage = _userSession.RecipeDraft!.Stages[0];
        var request = CreateRequest(callbackData: "edit:stage:delete:photo");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_userSession.CurrentViewStage.UrlImage, Is.Null);
            Assert.That(result!.UrlImage, Is.Null);
            Assert.That(_userSession.RecipeState, Is.EqualTo(RecipeStates.WaitingMoveForEditStage));
        });
    }

    [Test]
    public async Task AddStageCallback()
    {
        // Arrange
        _userSession.RecipeState = RecipeStates.WaitingForSelectStage;
        var request = CreateRequest(callbackData: "edit:stages:add");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_userSession.RecipeState, Is.EqualTo(RecipeStates.AddingStage));
            Assert.That(result!.Message, Does.Contain("В первой строке укажите номер этапа"));
            Assert.That(GetCallbacks(result), Is.EqualTo(new[] { "edit:stages", "edit:cancel" }));
        });
    }

    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(3, false)]
    public async Task AddStage(int number, bool useCaption)
    {
        // Arrange
        _userSession.RecipeState = RecipeStates.AddingStage;
        var previousStages = _userSession.RecipeDraft!.Stages.ToArray();
        var description = _fixture.Create<string>();
        var imageFileId = _fixture.Create<string>();
        var text = $" {number} \n {description} ";
        var request = useCaption
            ? CreateRequest(caption: text, imageFileId: imageFileId)
            : CreateRequest(text: text, imageFileId: imageFileId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var stages = _userSession.RecipeDraft.Stages;
        Assert.Multiple(() =>
        {
            Assert.That(stages.Select(x => x.Number), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(stages[number - 1].Description, Is.EqualTo(description));
            Assert.That(stages[number - 1].UrlImage, Is.EqualTo(imageFileId));
            Assert.That(stages.Where(x => x.Number != number), Is.EqualTo(previousStages));
            Assert.That(_userSession.CurrentViewStage, Is.Null);
            Assert.That(_userSession.RecipeState, Is.EqualTo(RecipeStates.WaitingForSelectStage));
            Assert.That(result!.Message, Is.EqualTo("✅ Выберите этап."));
        });
        _recipeRepositoryMock.VerifyNoOtherCalls();
    }

    [TestCase("abc\nОписание")]
    [TestCase("0\nОписание")]
    [TestCase("-1\nОписание")]
    [TestCase("4\nОписание")]
    [TestCase("1")]
    [TestCase("1\n  ")]
    public async Task InvalidStage(string text)
    {
        // Arrange
        _userSession.RecipeState = RecipeStates.AddingStage;
        var stages = _userSession.RecipeDraft!.Stages.ToArray();
        var request = CreateRequest(text: text);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result!.Message, Is.EqualTo(text.StartsWith("1")
                ? "После номера этапа введите описание с новой строки."
                : "В первой строке укажите номер этапа от 1 до 3, в следующих строках — описание."));
            Assert.That(_userSession.RecipeDraft.Stages, Is.EqualTo(stages));
            Assert.That(stages.Select(x => x.Number), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(_userSession.RecipeState, Is.EqualTo(RecipeStates.AddingStage));
        });
    }

    [Test]
    public async Task DeleteStage()
    {
        // Arrange
        _userSession.RecipeState = RecipeStates.WaitingMoveForEditStage;
        _userSession.CurrentViewStage = _userSession.RecipeDraft!.Stages[0];
        var remainingStage = _userSession.RecipeDraft.Stages[1];
        var request = CreateRequest(callbackData: "edit:stage:delete");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_userSession.RecipeDraft.Stages, Is.EqualTo(new[] { remainingStage }));
            Assert.That(remainingStage.Number, Is.EqualTo(1));
            Assert.That(_userSession.CurrentViewStage, Is.Null);
            Assert.That(_userSession.RecipeState, Is.EqualTo(RecipeStates.WaitingForSelectStage));
            Assert.That(GetCallbacks(result!), Does.Not.Contain("edit:stage:2"));
        });
        _recipeRepositoryMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task SaveStage()
    {
        // Arrange
        _userSession.RecipeState = RecipeStates.WaitingMoveForEditStage;
        var stage = CreateStage(1);
        _userSession.CurrentViewStage = stage;
        var request = CreateRequest(callbackData: "edit:stage:save");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_userSession.RecipeDraft!.Stages, Has.Count.EqualTo(2));
            Assert.That(_userSession.RecipeDraft.Stages[0], Is.SameAs(stage));
            Assert.That(_userSession.RecipeDraft.Stages.Select(x => x.Number), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(_userSession.RecipeState, Is.EqualTo(RecipeStates.WaitingForSelectStage));
            Assert.That(result!.Message, Is.EqualTo("✅ Выберите этап."));
        });
        _recipeRepositoryMock.VerifyNoOtherCalls();
    }

    [TestCase("id")]
    [TestCase("name")]
    [TestCase("ingredients")]
    [TestCase("stages")]
    [TestCase("description")]
    public async Task SaveIncompleteRecipe(string invalidField)
    {
        // Arrange
        var draft = _userSession.RecipeDraft!;
        switch (invalidField)
        {
            case "id": draft.Id = 0; break;
            case "name": draft.Name = " "; break;
            case "ingredients": draft.Ingredients = " "; break;
            case "stages": draft.Stages.Clear(); break;
            case "description": draft.Stages[1].Description = " "; break;
        }
        var request = CreateRequest(callbackData: "edit:save");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result!.Message,
                Is.EqualTo("Заполните название, ингредиенты и хотя бы один этап с описанием."));
            Assert.That(_userSession.UserState, Is.EqualTo(UserStates.EditRecipe));
            Assert.That(_userSession.RecipeDraft, Is.SameAs(draft));
        });
        _categoryRepositoryMock.VerifyNoOtherCalls();
        _recipeRepositoryMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task SaveUnavailableCategory()
    {
        // Arrange
        _categoryRepositoryMock.Setup(x => x.GetAllCategories(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var request = CreateRequest(callbackData: "edit:save");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result!.Message,
                Is.EqualTo("Категория больше недоступна. Выберите другую категорию."));
            Assert.That(_userSession.UserState, Is.EqualTo(UserStates.EditRecipe));
            Assert.That(GetCallbacks(result), Does.Contain("edit:category"));
        });
        _recipeRepositoryMock.VerifyNoOtherCalls();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SaveRecipe(bool recipeDeleted)
    {
        // Arrange
        var draft = _userSession.RecipeDraft!;
        var category = CreateCategory();
        using var cancellation = new CancellationTokenSource();
        _categoryRepositoryMock.Setup(x => x.GetAllCategories(cancellation.Token))
            .ReturnsAsync([category]);
        var update = _recipeRepositoryMock.Setup(x => x.Update(
            It.IsAny<Recipe>(), _chatId, draft.Id, cancellation.Token));
        if (recipeDeleted)
            update.ThrowsAsync(new KeyNotFoundException());
        else
            update.ReturnsAsync(new Recipe());
        var request = CreateRequest(callbackData: "edit:save");

        // Act
        var result = await _handler.Handle(request, cancellation.Token);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result!.Message, Is.EqualTo(recipeDeleted
                ? "Рецепт больше недоступен. Изменения не сохранены; черновик сохранён в сессии."
                : "✅ Изменения рецепта сохранены."));
            Assert.That(_userSession.UserState,
                Is.EqualTo(recipeDeleted ? UserStates.EditRecipe : UserStates.GetMyRecipes));
            Assert.That(_userSession.RecipeState,
                Is.EqualTo(recipeDeleted ? RecipeStates.WaitingMove : RecipeStates.ShowRecipeSelectionOptions));
            Assert.That(_userSession.RecipeDraft, Is.SameAs(draft));
            Assert.That(GetCallbacks(result), Does.Contain(recipeDeleted ? "edit:save" : "my_recipe"));
        });
        _recipeRepositoryMock.Verify(x => x.Update(
            It.Is<Recipe>(recipe => recipe.Name == draft.Name &&
                recipe.Ingredients == draft.Ingredients && recipe.CategoryId == draft.CategoryId &&
                recipe.UrlImage == draft.UrlImage && recipe.Stages.Count == draft.Stages.Count &&
                recipe.Stages[0].Description == draft.Stages[0].Description &&
                recipe.Stages[0].Number == draft.Stages[0].Number &&
                recipe.Stages[0].UrlImage == draft.Stages[0].UrlImage),
            _chatId, draft.Id, cancellation.Token), Times.Once);
        _userSessionStoreMock.Verify(x => x.SetUserSession(_chatId, _userSession), Times.AtLeastOnce);
    }

    #region Helpers

    private EditRecipeRequest CreateRequest(
        string? text = null,
        string? imageFileId = null,
        string? callbackData = null,
        string? caption = null)
    {
        return new EditRecipeRequest(
            ChatId: _chatId,
            Text: text,
            ImageFileId: imageFileId,
            CallbackData: callbackData,
            Caption: caption,
            UpdateId: _updateId);
    }

    private StageDto CreateStage(int number)
    {
        return _fixture.Build<StageDto>()
            .With(x => x.Number, number)
            .Create();
    }

    private Category CreateCategory()
    {
        var category = new Category();
        category.SetName(_fixture.Create<string>());
        typeof(Category).GetProperty(nameof(Category.Id))!
            .SetValue(category, _userSession.RecipeDraft!.CategoryId);
        return category;
    }

    private static string?[] GetCallbacks(HandlerResult result)
    {
        return result.Buttons!.InlineKeyboard
            .SelectMany(row => row)
            .Select(button => button.CallbackData)
            .ToArray();
    }

    #endregion
}
