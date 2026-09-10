using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RecipeBot.Application;
using RecipeBot.Application.Abstract;
using RecipeBot.Application.Handlers;
using RecipeBot.Application.Models;
using RecipeBot.Domain.Enums;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace RecipeBot.Telegram.BackGroundServices;

public class TelegramBotService(
    ILogger<TelegramBotService> logger,
    IMediator mediator,
    ITelegramBotClient botClient,
    IUserSessionStore userSessionStore)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var commands = new[]
        {
            new BotCommand
            {
                Command = "start",
                Description = "Запустить бота"
            },
            new BotCommand
            {
                Command = "cancel",
                Description = "Отменить действие"
            }
        };

        await botClient.SetMyCommands(commands, cancellationToken: stoppingToken);
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates =
            [
                UpdateType.Message,
                UpdateType.CallbackQuery
            ]
        };

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken: stoppingToken
        );
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        var result = new HandlerResult("", null,null);
        var chatId = 0L;
        string? text = null;
        string? imageFileId = null;
        string? callbackData = null;
        string? caption = null;

        if (!string.IsNullOrWhiteSpace(update.Message?.Text) || !string.IsNullOrWhiteSpace(update.Message?.Caption) || update.Message?.Photo != null)
        {
            chatId = update.Message.Chat.Id;
            text = update.Message.Text;
            caption = update.Message.Caption;
            if (update.Message.Photo != null && !string.IsNullOrWhiteSpace(update.Message.Photo.Last().FileId))
            {
                imageFileId = update.Message.Photo.Last().FileId;
            }

            var userSession = await userSessionStore.TryGetSession(chatId);
            if ( userSession == null )
            {
                await mediator.Send(new GetOrCreateUserRequest(chatId, update.Message.From.FirstName), cancellationToken);
            }

            switch (update.Message?.Text)
            {
                case "/start":
                    result = await mediator.Send(new StartMenuRequest(chatId, text, callbackData), cancellationToken);
                    break;
                case "/cancel":
                    userSession.UserState = UserStates.Cansel;
                    await userSessionStore.SetUserSession(chatId, userSession);
                    result = await Cancel(chatId, text, callbackData, cancellationToken);
                    break;
            }

            if (userSession != null)
            {
                switch (userSession.UserState)
                {
                    case UserStates.Waiting:
                        await bot.DeleteMessage(chatId, update.Message.MessageId, cancellationToken: cancellationToken);
                        await bot.SendMessage(
                            chatId,
                            "Нажмите на одну из кнопок",
                            cancellationToken: cancellationToken
                        );
                        break;
                    case UserStates.AddingRecipe:
                        result = await mediator.Send(
                            new AddNewRecipeRequest(chatId, text, imageFileId, callbackData, caption, update.Id),
                            cancellationToken);
                        break;
                    case UserStates.GetMyRecipes:
                        result = await mediator.Send(new GetUserRecipesRequest(chatId, text, callbackData, update.Id),
                            cancellationToken);
                        break;
                }
            }
        }
        else if (update.CallbackQuery != null)
        {
            var callback = update.CallbackQuery;
            callbackData = callback.Data;

            chatId = callback.Message.Chat.Id;
            var userSession = await userSessionStore.TryGetSession(chatId);

            if (userSession !=null )
            {
                switch (userSession.UserState)
                {
                    case UserStates.Waiting:

                        await bot.SendMessage(
                            chatId,
                            "Нажмите на одну из кнопок",
                            cancellationToken: cancellationToken
                        );
                        break;
                    case UserStates.AddingRecipe:
                        result = await mediator.Send(
                            new AddNewRecipeRequest(chatId, text, imageFileId, callbackData, caption, update.Id),
                            cancellationToken);
                        break;
                    case UserStates.GetMyRecipes:
                        result = await mediator.Send(new GetUserRecipesRequest(chatId, text, callbackData, update.Id),
                            cancellationToken);
                        break;
                }
            }

            var newSession = new UserSession();
            switch (callbackData)
            {
                case "add_recipe":
                    result = new HandlerResult("✏️ Введите наименование рецепта:", null,null);
                    newSession = new UserSession();
                    newSession.UserState = UserStates.AddingRecipe;
                    newSession.RecipeState = RecipeStates.WaitingForRecipeName;
                    await userSessionStore.SetUserSession(chatId, newSession);
                    break;
                case "my_recipe":
                    newSession = new UserSession();
                    newSession.UserState = UserStates.GetMyRecipes;
                    newSession.RecipeState = RecipeStates.ShowRecipeSelectionOptions;
                    await userSessionStore.SetUserSession(chatId, newSession);
                    result = await mediator.Send(new GetUserRecipesRequest(chatId, text, callbackData, update.Id),
                        cancellationToken);
                    
                    break;
                case "cancel":
                    result = await Cancel(chatId, text, callbackData, cancellationToken);
                    break;
            }
            
            await bot.AnswerCallbackQuery(
                update.CallbackQuery.Id,
                cancellationToken: cancellationToken);
        }
        else
        {
            result = new HandlerResult("⚠️ Вы ещё не ввели текст.", null,null);
        }
       
        await SendMessage(bot, chatId, result, update.Id, cancellationToken);
    }

    private async Task SendMessage(ITelegramBotClient bot, long chatId, HandlerResult? result, int updateId,
        CancellationToken cancellationToken)
    {
        if (result == null) return;
        if (string.IsNullOrEmpty(result.UrlImage)) 
        {
            await bot.SendMessage(
            chatId,
            text: result.Message,
            replyMarkup: result.Buttons,
            cancellationToken: cancellationToken);
        }
        else
        {
            await bot.SendPhoto(
                chatId,
                InputFile.FromFileId(result.UrlImage),
                caption: result.Message,
                replyMarkup: result.Buttons,
                cancellationToken: cancellationToken);
        }
        var session = await userSessionStore.TryGetSession(chatId);
        session.CurrentUpdateId = null;
        session.LastUpdateId = updateId;
        await userSessionStore.SetUserSession(chatId, session);
        await userSessionStore.RemoveProcessing(chatId);
    }

    private async Task<HandlerResult?> Cancel(long chatId, string? text, string? callBack,
        CancellationToken cancellationToken)
    {
        return await mediator.Send(new StartMenuRequest(chatId, text, callBack), cancellationToken);
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiEx => $"Telegram API Error:\n[{apiEx.ErrorCode}]\n{apiEx.Message}",
            _ => exception.ToString()
        };

        logger.LogError(errorMessage);
        return Task.CompletedTask;
    }
}