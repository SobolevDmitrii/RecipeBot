using Microsoft.Extensions.Hosting;
using RecipeBot.Application.Abstract;
using Telegram.Bot;

namespace RecipeBot.Telegram.BackGroundServices;

public class SessionRecoveryService(IRedisService redisService,
    ITelegramBotClient botClient) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var nonActivitySession = await redisService.GetExpiredProcessingAsync(5);

            var sessions = await redisService.GetUserSessionsAsync(nonActivitySession);

            var crashSessions = sessions.Where(q => q?.CurrentUpdateId != null)
                .ToList();

            foreach (var session in crashSessions)
            {
                try
                {
                    await botClient.SendMessage(
                        session.ChatId,
                        text: "Произошли технические неполадки. Воспользуйтесь командой '/start' или '/cancel' чтобы начать заново.",
                        cancellationToken: stoppingToken);
                }
                catch (Exception e)
                {
                    //ToDo потом тут будет логирование
                }
            }

            if (crashSessions.Count > 0)
            {
                await redisService.RemoveRangeProcessingAsync(crashSessions.Select(q=>q.ChatId).ToList());
            }
            
            await Task.Delay(
                TimeSpan.FromMinutes(5),
                stoppingToken);
        }
    }
}