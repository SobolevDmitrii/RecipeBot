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
public class GetRecipeTests
{
    private Mock<IMediator> _mediatorMock = null!;
    private Mock<IRecipeRepository> _recipeRepositoryMock = null!;
    private Mock<ICategoryRepository> _categoryRepositoryMock = null!;
    private Mock<IUserSessionStore> _userSessionStoreMock = null!;
    private const long ChatId = 12345;
    private const int UpdateId = 100;
    
    private AddNewRecipeHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _mediatorMock = new Mock<IMediator>();
        _recipeRepositoryMock = new Mock<IRecipeRepository>();
        _categoryRepositoryMock = new Mock<ICategoryRepository>();
        _userSessionStoreMock = new Mock<IUserSessionStore>();

        _handler = new AddNewRecipeHandler(
            _mediatorMock.Object,
            _recipeRepositoryMock.Object,
            _categoryRepositoryMock.Object,
            _userSessionStoreMock.Object);
    }

    [Test]
    public async Task Handle_WhenWaitingForRecipeName_ShouldCreateRecipeDraftAndMoveToCategoryState()
    {
        // Arrange
        
        const string recipeName = "Борщ";

        var userSession = new UserSession
        {
            RecipeState = RecipeStates.WaitingForRecipeName
        };

        _userSessionStoreMock
            .Setup(x => x.TryGetSession(ChatId))
            .ReturnsAsync(userSession);

        _categoryRepositoryMock
            .Setup(x => x.GetAllCategories(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category>());
        

        var request = new AddNewRecipeRequest(
            ChatId: ChatId,
            Text: recipeName,
            ImageFileId: null,
            CallbackData: null,
            Caption: null,
            UpdateId: UpdateId);

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
                userSession.RecipeState,
                Is.EqualTo(RecipeStates.WaitingForCategory));

            Assert.That(
                userSession.RecipeDraft,
                Is.Not.Null);

            Assert.That(
                userSession.RecipeDraft!.Name,
                Is.EqualTo(recipeName));

            Assert.That(
                userSession.ChatId,
                Is.EqualTo(ChatId));

            Assert.That(
                userSession.CurrentUpdateId,
                Is.EqualTo(UpdateId));
        });

        _userSessionStoreMock.Verify(
            x => x.AddProcessing(ChatId),
            Times.Once);

        _userSessionStoreMock.Verify(
            x => x.SetUserSession(
                ChatId,
                It.Is<UserSession>(session =>
                    session.RecipeState == RecipeStates.WaitingForCategory &&
                    session.RecipeDraft != null &&
                    session.RecipeDraft.Name == recipeName)),
            Times.AtLeastOnce);

        _recipeRepositoryMock.Verify(
            x => x.Add(
                It.IsAny<Recipe>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}