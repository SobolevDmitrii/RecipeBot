using System.Reflection;
using AutoFixture;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RecipeBot.Application.Abstract;
using RecipeBot.Application.Handlers;
using RecipeBot.Application.Models;
using RecipeBot.Domain.Enums;
using RecipeBot.Telegram.BackGroundServices;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace RecipeBot.UnitTests;

[TestFixture]
public class TelegramBotServiceTests
{
    private Fixture _fixture = null!;

    private Mock<ILogger<TelegramBotService>> _loggerMock = null!;
    private Mock<IServiceScopeFactory> _scopeFactory = null!;
    private Mock<ITelegramBotClient> _botClientMock = null!;
    private Mock<IMediator> _mediatorMock = null!;
    private Mock<IUserSessionStore> _userSessionStoreMock = null!;

    private TelegramBotService _service = null!;

    private long _chatId;
    private int _updateId;

    [SetUp]
    public void SetUp()
    {
        _fixture = new Fixture();

        _loggerMock = new Mock<ILogger<TelegramBotService>>();
        _scopeFactory = new Mock<IServiceScopeFactory>();
        _botClientMock = new Mock<ITelegramBotClient>();
        _mediatorMock = new Mock<IMediator>();
        _userSessionStoreMock = new Mock<IUserSessionStore>();

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(x => x.GetService(typeof(IMediator)))
            .Returns(_mediatorMock.Object);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(serviceProvider.Object);
        _scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        _chatId = _fixture.Create<long>();
        _updateId = _fixture.Create<int>();

        _service = new TelegramBotService(
            _loggerMock.Object,
            _scopeFactory.Object,
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
            Times.Exactly(2));

        _mediatorMock.Verify(
            x => x.Send(
                It.Is<StartMenuRequest>(request =>
                    request.ChatId == _chatId &&
                    request.Text == "/cancel"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task CallbackWhileAddingRecipe()
    {
        // Arrange
        var callbackData = _fixture.Create<string>();
        _userSessionStoreMock.Setup(x => x.TryGetSession(_chatId))
            .ReturnsAsync(new UserSession { UserState = UserStates.AddingRecipe });
        _mediatorMock.Setup(x => x.Send(It.IsAny<AddNewRecipeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HandlerResult?)null);
        var update = CreateCallbackUpdate(callbackData);

        // Act
        await InvokeHandleUpdateAsync(update);

        // Assert
        _mediatorMock.Verify(x => x.Send(
            It.Is<AddNewRecipeRequest>(request =>
                request.ChatId == _chatId && request.CallbackData == callbackData &&
                request.UpdateId == _updateId && request.Text == null &&
                request.ImageFileId == null && request.Caption == null),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.VerifyNoOtherCalls();
        AssertCallbackAnswered(update);
    }

    [Test]
    public async Task CallbackWhileGettingMyRecipes()
    {
        // Arrange
        var callbackData = _fixture.Create<string>();
        _userSessionStoreMock.Setup(x => x.TryGetSession(_chatId))
            .ReturnsAsync(new UserSession { UserState = UserStates.GetMyRecipes });
        _mediatorMock.Setup(x => x.Send(It.IsAny<GetUserRecipesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HandlerResult?)null);
        var update = CreateCallbackUpdate(callbackData);

        // Act
        await InvokeHandleUpdateAsync(update);

        // Assert
        _mediatorMock.Verify(x => x.Send(
            It.Is<GetUserRecipesRequest>(request =>
                request.ChatId == _chatId && request.CallbackData == callbackData &&
                request.UpdateId == _updateId && request.text == null),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.VerifyNoOtherCalls();
        AssertCallbackAnswered(update);
    }

    [Test]
    public async Task CallbackWhileWaiting()
    {
        // Arrange
        _userSessionStoreMock.Setup(x => x.TryGetSession(_chatId))
            .ReturnsAsync(new UserSession { UserState = UserStates.Waiting });
        var update = CreateCallbackUpdate(_fixture.Create<string>());

        // Act
        await InvokeHandleUpdateAsync(update);

        // Assert
        var messages = _botClientMock.Invocations.SelectMany(x => x.Arguments)
            .OfType<SendMessageRequest>();
        Assert.That(messages.Count(request => request.ChatId.Identifier == _chatId &&
            request.Text == "Нажмите на одну из кнопок"), Is.EqualTo(1));
        _mediatorMock.VerifyNoOtherCalls();
        AssertCallbackAnswered(update);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task AddRecipeCallback(bool hasSession)
    {
        // Arrange
        UserSession? session = hasSession ? new UserSession { UserState = UserStates.Start } : null;
        _userSessionStoreMock.Setup(x => x.TryGetSession(_chatId))
            .ReturnsAsync(() => session);
        _userSessionStoreMock.Setup(x => x.SetUserSession(_chatId, It.IsAny<UserSession>()))
            .Callback<long, UserSession>((_, value) => session = value)
            .Returns(Task.CompletedTask);
        var update = CreateCallbackUpdate("add_recipe");

        // Act
        await InvokeHandleUpdateAsync(update);

        // Assert
        Assert.That(session, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(session!.UserState, Is.EqualTo(UserStates.AddingRecipe));
            Assert.That(session.RecipeState, Is.EqualTo(RecipeStates.WaitingForRecipeName));
        });
        _userSessionStoreMock.Verify(x => x.SetUserSession(_chatId,
            It.Is<UserSession>(value => value.UserState == UserStates.AddingRecipe &&
                value.RecipeState == RecipeStates.WaitingForRecipeName)), Times.AtLeastOnce);
        var message = _botClientMock.Invocations.SelectMany(x => x.Arguments)
            .OfType<SendMessageRequest>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(message.ChatId.Identifier, Is.EqualTo(_chatId));
            Assert.That(message.Text, Is.EqualTo("✏️ Введите наименование рецепта:"));
        });
        _mediatorMock.VerifyNoOtherCalls();
        AssertCallbackAnswered(update);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task MyRecipesCallback(bool hasSession)
    {
        // Arrange
        UserSession? session = hasSession ? new UserSession { UserState = UserStates.Start } : null;
        _userSessionStoreMock.Setup(x => x.TryGetSession(_chatId))
            .ReturnsAsync(() => session);
        _userSessionStoreMock.Setup(x => x.SetUserSession(_chatId, It.IsAny<UserSession>()))
            .Callback<long, UserSession>((_, value) => session = value)
            .Returns(Task.CompletedTask);
        _mediatorMock.Setup(x => x.Send(It.IsAny<GetUserRecipesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HandlerResult?)null);
        var update = CreateCallbackUpdate("my_recipe");

        // Act
        await InvokeHandleUpdateAsync(update);

        // Assert
        _userSessionStoreMock.Verify(x => x.SetUserSession(_chatId,
            It.Is<UserSession>(session => session.UserState == UserStates.GetMyRecipes &&
                session.RecipeState == RecipeStates.ShowRecipeSelectionOptions)), Times.Exactly(2));
        _mediatorMock.Verify(x => x.Send(
            It.Is<GetUserRecipesRequest>(request => request.ChatId == _chatId &&
                request.text == null && request.CallbackData == "my_recipe" && request.UpdateId == _updateId),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.VerifyNoOtherCalls();
        AssertCallbackAnswered(update);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task CancelCallback(bool hasSession)
    {
        // Arrange
        UserSession? session = hasSession ? new UserSession { UserState = UserStates.Start } : null;
        _userSessionStoreMock.Setup(x => x.TryGetSession(_chatId))
            .ReturnsAsync(() => session);
        _mediatorMock.Setup(x => x.Send(It.IsAny<StartMenuRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => session ??= new UserSession { UserState = UserStates.Start })
            .ReturnsAsync((HandlerResult?)null);
        var update = CreateCallbackUpdate("cancel");

        // Act
        await InvokeHandleUpdateAsync(update);

        // Assert
        _mediatorMock.Verify(x => x.Send(
            It.Is<StartMenuRequest>(request => request.ChatId == _chatId &&
                request.Text == null && request.CallBack == "cancel"),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.VerifyNoOtherCalls();
        AssertCallbackAnswered(update);
    }

    private void AssertCallbackAnswered(Update update)
    {
        var answer = _botClientMock.Invocations.SelectMany(x => x.Arguments)
            .OfType<AnswerCallbackQueryRequest>().Single();
        Assert.That(answer.CallbackQueryId, Is.EqualTo(update.CallbackQuery!.Id));
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
                Id = _fixture.Create<string>(),
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
