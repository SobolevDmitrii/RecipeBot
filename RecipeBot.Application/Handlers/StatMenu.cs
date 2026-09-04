using MediatR;
using RecipeBot.Application.Models;
using RecipeBot.Domain.Enums;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace RecipeBot.Application.Handlers;

public record StartMenuRequest(
    long ChatId,
    string? Text,
    string? CallBack
    ) : IRequest<HandlerResult?>;

public class StartMenuHandler() : IRequestHandler<StartMenuRequest,HandlerResult?>
{
    public async Task<HandlerResult?> Handle(StartMenuRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text) && string.IsNullOrWhiteSpace(request.CallBack))
        {
            return null;
        }

        var session = Dictionaries.UserSessions.GetOrAdd(
            request.ChatId,
            _ => new UserSession());
        session.UserState = States.Start;
        //await botClient.DeleteMessage(chatId, mesId, cancellationToken: cancellationToken);
        /*var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📄 Новый рецепт", "add_recipe"),
                InlineKeyboardButton.WithCallbackData("📄 Мои рецепты", "my_recipe"),
                InlineKeyboardButton.WithCallbackData("⚙️ Настройки", "confirm_no")
            }
        });
        await botClient.SendMessage(
            chatId: chatId,
            text: "Выберите действие:",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken
        );*/

        var buttons = new[]
        {
            InlineKeyboardButton.WithCallbackData("📄 Новый рецепт", "add_recipe"),
            InlineKeyboardButton.WithCallbackData("📄 Мои рецепты", "my_recipe"),
            InlineKeyboardButton.WithCallbackData("⚙️ Настройки", "confirm_no")
        };
        return new HandlerResult("Выберите действие:",new InlineKeyboardMarkup(buttons));
    }
}
