using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RecipeBot.Application.Handlers;
using RecipeBot.Domain.Enums;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace RecipeBot.Application.BackGroundServices;

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
        
        if (!string.IsNullOrWhiteSpace(update.Message?.Text))
        {
            var chatId = update.Message.Chat.Id;

            if (Dictionaries.UserState.TryGetValue(chatId, out var state))
            {
                switch (state)
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
                      await mediator.Send(new AddNewRecipeRequest(update), cancellationToken);
                      break;
                }
               
            }

            
            switch (update.Message?.Text)
            {
                case "/start":
                    await mediator.Send(new StartMenuRequest(update), cancellationToken);
                    break;
            }
        }

        if (update.CallbackQuery != null)
        {
            var callback = update.CallbackQuery;
            var callbackData = callback.Data;

            var chatId = callback.Message.Chat.Id;

            if (Dictionaries.UserState.TryGetValue(chatId, out var state))
            {
                switch (state)
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
                        await mediator.Send(new AddNewRecipeRequest(update), cancellationToken);
                        break;
                }
               
            }
            switch (callbackData)
            {
                case "add_recipe":
                    await bot.EditMessageReplyMarkup(chatId, callback.Message.MessageId,
                        new InlineKeyboardMarkup());
                    await bot.DeleteMessage(chatId, callback.Message.MessageId);
                    await bot.SendTextMessageAsync(
                        chatId,
                        "✏️ Введите наименование рецепта:",
                        cancellationToken: cancellationToken
                    );
                    Dictionaries.UserState[chatId] = States.AddingRecipe;
                    Dictionaries.RecipeState[chatId] = States.WaitingForRecipeName;
                    break;
            }
        }


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