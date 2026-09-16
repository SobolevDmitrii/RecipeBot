using System.Reflection;
using AutoFixture;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using RecipeBot.Application.Abstract;
using RecipeBot.Application.Handlers;
using RecipeBot.Application.Models;
using RecipeBot.Domain.Enums;
using RecipeBot.Telegram.BackGroundServices;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace RecipeBot.UnitTests;

[TestFixture]
public class TelegramBotServiceTests
{
    private Fixture _fixture = null!;

    private Mock<ILogger<TelegramBotService>> _loggerMock = null!;
    private Mock<IMediator> _mediatorMock = null!;
    private Mock<ITelegramBotClient> _botClientMock = null!;
    private Mock<IUserSessionStore> _userSessionStoreMock = null!;

    private TelegramBotService _service = null!;

    private long _chatId;
    private int _updateId;

    [SetUp]
    public void SetUp()
    {
        _fixture = new Fixture();

        _loggerMock = new Mock<ILogger<TelegramBotService>>();
        _mediatorMock = new Mock<IMediator>();
        _botClientMock = new Mock<ITelegramBotClient>();
        _userSessionStoreMock = new Mock<IUserSessionStore>();

        _chatId = _fixture.Create<long>();
        _updateId = _fixture.Create<int>();

        _service = new TelegramBotService(
            _loggerMock.Object,
            _mediatorMock.Object,
            _botClientMock.Object,
            _userSessionStoreMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    [Test]
    public async Task StartCommand()
    {
        // Arrange
        var session = new UserSession
        {
            ChatId = _chatId,
            UserState = UserStates.Waiting
        };

        _userSessionStoreMock
            .Setup(x => x.TryGetSession(_chatId))
            .ReturnsAsync(session);

        _mediatorMock
            .Setup(x => x.Send(
                It.IsAny<StartMenuRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((HandlerResult?)null);

        var update = CreateMessageUpdate("/start");

        // Act
        await InvokeHandleUpdateAsync(update);

        // Assert
        _mediatorMock.Verify(
            x => x.Send(
                It.Is<StartMenuRequest>(request =>
                    request.ChatId == _chatId &&
                    request.Text == "/start"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.That(
            session.UserState,
            Is.EqualTo(UserStates.Start));
    }

    [Test]
    public async Task AddingRecipe()
    {
        // Arrange
        var text = _fixture.Create<string>();

        var session = new UserSession
        {
            ChatId = _chatId,
            UserState = UserStates.AddingRecipe,
            RecipeState = RecipeStates.WaitingForRecipeName
        };

        _userSessionStoreMock
            .Setup(x => x.TryGetSession(_chatId))
            .ReturnsAsync(session);

        _mediatorMock
            .Setup(x => x.Send(
                It.IsAny<AddNewRecipeRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((HandlerResult?)null);

        var update = CreateMessageUpdate(text);

        // Act
        await InvokeHandleUpdateAsync(update);

        // Assert
        _mediatorMock.Verify(
            x => x.Send(
                It.Is<AddNewRecipeRequest>(request =>
                    request.ChatId == _chatId &&
                    request.Text == text &&
                    request.UpdateId == _updateId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task GetMyRecipes()
    {
        // Arrange
        var text = _fixture.Create<string>();

        var session = new UserSession
        {
            ChatId = _chatId,
            UserState = UserStates.GetMyRecipes,
            RecipeState = RecipeStates.ShowRecipeSelectionOptions
        };

        _userSessionStoreMock
            .Setup(x => x.TryGetSession(_chatId))
            .ReturnsAsync(session);

        _mediatorMock
            .Setup(x => x.Send(
                It.IsAny<GetUserRecipesRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((HandlerResult?)null);

        var update = CreateMessageUpdate(text);

        // Act
        await InvokeHandleUpdateAsync(update);

        // Assert
        _mediatorMock.Verify(
            x => x.Send(
                It.Is<GetUserRecipesRequest>(request =>
                    request.ChatId == _chatId &&
                    request.text == text &&
                    request.UpdateId == _updateId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task CancelCommand()
    {
        // Arrange
        var session = new UserSession
        {
            ChatId = _chatId,
            UserState = UserStates.AddingRecipe
        };

        _userSessionStoreMock
            .Setup(x => x.TryGetSession(_chatId))
            .ReturnsAsync(session);

        _mediatorMock
            .Setup(x => x.Send(
                It.IsAny<StartMenuRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((HandlerResult?)null);

        var update = CreateMessageUpdate("/cancel");

        // Act
        await InvokeHandleUpdateAsync(update);

        // Assert
        Assert.That(
            session.UserState,
            Is.EqualTo(UserStates.Cansel));

        _userSessionStoreMock.Verify(
            x => x.SetUserSession(
                _chatId,
                session),
            Times.Once);

        _mediatorMock.Verify(
            x => x.Send(
                It.Is<StartMenuRequest>(request =>
                    request.ChatId == _chatId &&
                    request.Text == "/cancel"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private async Task InvokeHandleUpdateAsync(Update update)
    {
        var method = typeof(TelegramBotService)
            .GetMethod(
                "HandleUpdateAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(
            method,
            Is.Not.Null,
            "Метод HandleUpdateAsync не найден");

        var task = method!.Invoke(
            _service,
            [
                _botClientMock.Object,
                update,
                CancellationToken.None
            ]) as Task;

        Assert.That(
            task,
            Is.Not.Null,
            "HandleUpdateAsync не вернул Task");

        await task!;
    }

    private Update CreateMessageUpdate(string text)
    {
        return new Update
        {
            Id = _updateId,

            Message = new Message
            {
                Text = text,

                Chat = new Chat
                {
                    Id = _chatId
                }
            }
        };
    }

    private Update CreateCallbackUpdate(string text)
    {
        return new Update
        {
            Id = _updateId,

            CallbackQuery = new CallbackQuery()
            {
                Data = text,

                Message = new Message()
                {
                    Chat = new Chat
                    {
                        Id = _chatId
                    }
                }
            }
        };
    }
}