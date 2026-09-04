using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RecipeBot.Application;
using RecipeBot.Application.Handlers;
using RecipeBot.Application.Models;
using RecipeBot.Domain.Enums;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace RecipeBot.Telegram.BackGroundServices;

public class TelegramBotService(
    ILogger<TelegramBotService> logger,
    IMediator mediator,
    ITelegramBotClient botClient)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [] // получаем все типы обновлений
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
        var result = new HandlerResult("", null);
        var chatId =  0L;
        string? text = null;
        string? imageFileId = null;
        string? callBackData = null;
        string? caption = null;
        
        if (!string.IsNullOrWhiteSpace(update.Message?.Text) || !string.IsNullOrWhiteSpace(update.Message?.Caption) )
        {
             chatId = update.Message.Chat.Id;
             text = update.Message.Text;
             caption = update.Message.Caption;
             if (update.Message.Photo != null && !string.IsNullOrWhiteSpace(update.Message.Photo.Last().FileId))
             {
                 imageFileId = update.Message.Photo.Last().FileId;
             }
             switch (update.Message?.Text)
             {
                 case "/start":
                     if (!Dictionaries.UserSessions.TryGetValue(update.Message.Chat.Id, out var user))
                     {
                         await mediator.Send(new GetOrCreateUserRequest(update), cancellationToken);
                     }
                     result = await mediator.Send(new StartMenuRequest(chatId, text, callBackData), cancellationToken);
                     break;
             }

            if (Dictionaries.UserSessions.TryGetValue(chatId, out var session))
            {
                switch (session.UserState)
                {
                  case States.Waiting:
                      await bot.DeleteMessage(chatId, update.Message.MessageId, cancellationToken: cancellationToken);
                      await bot.SendMessage(
                          chatId,
                          "Нажмите на одну из кнопок",
                          cancellationToken: cancellationToken
                      );
                      break;
                  case States.AddingRecipe:
                      result = await mediator.Send(new AddNewRecipeRequest(chatId,text,imageFileId,callBackData, caption), cancellationToken);
                      break;
                }
               
            }
           
        } else if (update.CallbackQuery != null)
        {
            var callback = update.CallbackQuery;
            callBackData = callback.Data;

            chatId = callback.Message.Chat.Id;

            if (Dictionaries.UserSessions.TryGetValue(chatId, out var session))
            {
                switch (session.UserState)
                {
                    case States.Waiting:
                       
                        await bot.SendMessage(
                            chatId,
                            "Нажмите на одну из кнопок",
                            cancellationToken: cancellationToken
                        );
                        break;
                    case States.AddingRecipe:
                        result = await mediator.Send(new AddNewRecipeRequest(chatId,text,imageFileId,callBackData, caption), cancellationToken);
                        break;
                }
               
            }
            switch (callBackData)
            {
                case "add_recipe":
                   result = new HandlerResult("✏️ Введите наименование рецепта:",null);
                    Dictionaries.UserSessions[chatId] = new UserSession();
                    Dictionaries.UserSessions[chatId].UserState = States.AddingRecipe;
                    Dictionaries.UserSessions[chatId].RecipeState = States.WaitingForRecipeName;
                    break;
                case "cancel":
                    result = await Cancel(chatId,text,callBackData, cancellationToken);
                    break;
            }
        }
        else
        {
            result = new HandlerResult( "⚠️ Вы ещё не ввели текст.", null);
        }
        await SendMessage(bot,  chatId, result, cancellationToken);
        
    }

    private async Task SendMessage(ITelegramBotClient bot,long chatId, HandlerResult? result,
        CancellationToken cancellationToken)
    {
        if (result == null) return;
        await bot.SendMessage(
            chatId,
            text:result.Message,
            replyMarkup:result.Buttons,
            cancellationToken: cancellationToken
        );
    }

    private async Task<HandlerResult?> Cancel(long chatId,string? text, string? callBack, CancellationToken cancellationToken)
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