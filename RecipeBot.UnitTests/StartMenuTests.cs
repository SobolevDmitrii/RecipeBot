using AutoFixture;
using Moq;
using RecipeBot.Application.Abstract;
using RecipeBot.Application.Handlers;
using RecipeBot.Application.Models;
using RecipeBot.Domain.Enums;

namespace RecipeBot.UnitTests;

[TestFixture]
public class StartMenuTests
{
    private Fixture _fixture = null!;

    private Mock<IUserSessionStore> _userSessionStoreMock = null!;

    private StartMenuHandler _handler = null!;
    private UserSession _userSession = null!;

    private long _chatId;

    [SetUp]
    public void SetUp()
    {
        _fixture = new Fixture();

        _userSessionStoreMock =
            new Mock<IUserSessionStore>();

        _chatId = _fixture.Create<long>();

        _userSession = new UserSession();

        _userSessionStoreMock
            .Setup(x => x.GetOrAddUserSession(_chatId))
            .ReturnsAsync(_userSession);

        _handler =
            new StartMenuHandler(
                _userSessionStoreMock.Object);
    }

    [Test]
    public async Task TextAndCallbackIsNull()
    {
        // Arrange
        var request = CreateRequest();

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);

        _userSessionStoreMock.Verify(
            x => x.GetOrAddUserSession(
                It.IsAny<long>()),
            Times.Never);

        _userSessionStoreMock.Verify(
            x => x.SetUserSession(
                It.IsAny<long>(),
                It.IsAny<UserSession>()),
            Times.Never);
    }

    [Test]
    public async Task TextAndCallbackIsEmpty()
    {
        // Arrange
        var request = CreateRequest(
            text: string.Empty,
            callback: string.Empty);

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);

        _userSessionStoreMock.Verify(
            x => x.GetOrAddUserSession(
                It.IsAny<long>()),
            Times.Never);
    }

    [Test]
    public async Task TextIsWhiteSpace()
    {
        // Arrange
        var request = CreateRequest(
            text: "   ");

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);

        _userSessionStoreMock.Verify(
            x => x.GetOrAddUserSession(
                It.IsAny<long>()),
            Times.Never);
    }

    [Test]
    public async Task TextExists()
    {
        // Arrange
        var text = _fixture.Create<string>();

        var request = CreateRequest(
            text: text);

        // Act
        await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        _userSessionStoreMock.Verify(
            x => x.GetOrAddUserSession(_chatId),
            Times.Once);
    }

    [Test]
    public async Task CallbackExists()
    {
        // Arrange
        var callback = _fixture.Create<string>();

        var request = CreateRequest(
            callback: callback);

        // Act
        await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        _userSessionStoreMock.Verify(
            x => x.GetOrAddUserSession(_chatId),
            Times.Once);
    }

    [Test]
    public async Task RequestIsValid()
    {
        // Arrange
        _userSession.UserState =
            UserStates.AddingRecipe;

        var request = CreateRequest(
            text: _fixture.Create<string>());

        // Act
        var result = await _handler.Handle(
            request,
            CancellationToken.None);

        // Assert
        Assert.That(
            _userSession.UserState,
            Is.EqualTo(UserStates.Start));
        
        _userSessionStoreMock.Verify(
            x => x.SetUserSession(
                _chatId,
                It.Is<UserSession>(session =>
                    session.UserState ==
                    UserStates.Start)),
            Times.Once);
        
        Assert.Multiple(() =>
        {
            Assert.That(
                result,
                Is.Not.Null);

            Assert.That(
                result!.Message,
                Does.Contain("Выберите действие"));

            Assert.That(
                result.UrlImage,
                Is.Null);

            Assert.That(
                result.Buttons,
                Is.Not.Null);
        });
    }

    private StartMenuRequest CreateRequest(
        string? text = null,
        string? callback = null)
    {
        return new StartMenuRequest(
            ChatId: _chatId,
            Text: text,
            CallBack: callback);
    }
}