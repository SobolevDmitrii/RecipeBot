using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Telegram.Bot.Exceptions;

namespace RecipeBot.Telegram;
public record ExceptionHandlerRequest(Exception Exception) : IRequest;

public class ExceptionHandler(ILogger<ExceptionHandler> logger):IRequestHandler<ExceptionHandlerRequest>
{
    public Task Handle(ExceptionHandlerRequest request, CancellationToken cancellationToken)
    {
        var exception = request.Exception;
        
        var errorMessage = exception switch
        {
            ApiRequestException apiEx => $"Telegram API Error:\n[{apiEx.ErrorCode}]\n{apiEx.Message}",
            DbUpdateException dbEx =>  $"PostgreSql Error: {dbEx.Message} ",
            RedisTimeoutException or RedisConnectionException => $"Redis: {exception.Message}",
            _ => exception.ToString()
        };

        if (!string.IsNullOrEmpty(errorMessage))
        {
            logger.LogError(exception ,errorMessage);
        }
       
        return Task.CompletedTask;
    }
}