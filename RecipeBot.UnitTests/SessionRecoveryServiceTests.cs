using AutoFixture;
using MediatR;
using Moq;
using RecipeBot.Application.Abstract;
using RecipeBot.Application.Models;
using RecipeBot.Telegram.BackGroundServices;
using Telegram.Bot;
using Telegram.Bot.Requests;

namespace RecipeBot.UnitTests;

[TestFixture]
public class SessionRecoveryServiceTests
{
    private Fixture _fixture = null!;
    private Mock<IRedisService> _redisServiceMock = null!;
    private Mock<ITelegramBotClient> _botClientMock = null!;
    private Mock<IMediator> _meditorMock = null!;
    private TestableSessionRecoveryService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = new Fixture();

        _redisServiceMock = new Mock<IRedisService>();
        _botClientMock = new Mock<ITelegramBotClient>();
        _meditorMock =  new Mock<IMediator>();

        _service = new TestableSessionRecoveryService(
            _redisServiceMock.Object,
            _botClientMock.Object,
            _meditorMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    [Test]
    public async Task CrashSession()
    {
        // Arrange
        var chatId = _fixture.Create<long>();

        var session = new UserSession
        {
            ChatId = chatId,
            CurrentUpdateId = _fixture.Create<long>()
        };

        _redisServiceMock
            .Setup(x => x.GetExpiredProcessingAsync(5))
            .ReturnsAsync([chatId]);

        _redisServiceMock
            .Setup(x => x.GetUserSessionsAsync(
                It.IsAny<List<long>>()))
            .ReturnsAsync([session]);

        using var cts = new CancellationTokenSource();

        _redisServiceMock
            .Setup(x => x.RemoveRangeProcessingAsync(
                It.IsAny<List<long>>()))
            .Callback(() => cts.Cancel())
            .Returns(Task.CompletedTask);

        // Act
        try
        {
            await _service.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        // Assert
        var sendRequest = _botClientMock.Invocations
            .SelectMany(x => x.Arguments)
            .OfType<SendMessageRequest>()
            .Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                sendRequest.ChatId.Identifier,
                Is.EqualTo(chatId));

            Assert.That(
                sendRequest.Text,
                Is.EqualTo(
                    "Произошли технические неполадки. Воспользуйтесь командой '/start' или '/cancel' чтобы начать заново."));
        });
    }

    [Test]
    public async Task ActiveSessions()
    {
        // Arrange
        var chatId1 = _fixture.Create<long>();
        var chatId2 = _fixture.Create<long>();

        var sessions = new List<UserSession?>
        {
            new()
            {
                ChatId = chatId1,
                CurrentUpdateId = null
            },
            new()
            {
                ChatId = chatId2,
                CurrentUpdateId = null
            }
        };

        _redisServiceMock
            .Setup(x => x.GetExpiredProcessingAsync(5))
            .ReturnsAsync([chatId1, chatId2]);

        _redisServiceMock
            .Setup(x => x.GetUserSessionsAsync(
                It.IsAny<List<long>>()))
            .ReturnsAsync(sessions);

        using var cts = new CancellationTokenSource();

        cts.CancelAfter(
            TimeSpan.FromMilliseconds(100));

        // Act
        try
        {
            await _service.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        // Assert
        var sendRequests = _botClientMock.Invocations
            .SelectMany(x => x.Arguments)
            .OfType<SendMessageRequest>()
            .ToList();

        Assert.That(sendRequests, Is.Empty);

        _redisServiceMock.Verify(
            x => x.RemoveRangeProcessingAsync(
                It.IsAny<List<long>>()),
            Times.Never);
    }

    private sealed class TestableSessionRecoveryService
        : SessionRecoveryService
    {
        public TestableSessionRecoveryService(
            IRedisService redisService,
            ITelegramBotClient botClient,
            IMediator mediator)
            : base(redisService, botClient,mediator)
        {
        }

        public Task RunAsync(
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(cancellationToken);
        }
    }
}