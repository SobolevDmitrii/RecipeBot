using MediatR;
using RecipeBot.Domain.Enums;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace RecipeBot.Application.Handlers;

public class StartMenuRequest(Update update) : MessageBaseRequest(update);

public class StartMenuHandler(ITelegramBotClient botClient) : IRequestHandler<StartMenuRequest,Update>
{
    public async Task<Update> Handle(StartMenuRequest request, CancellationToken cancellationToken)
    {
        var update = request.Update;
        var chatId = 0L;
        if (!string.IsNullOrWhiteSpace(update.Message?.Text))
        {
            chatId = update.Message.Chat.Id;
        } else if(update.CallbackQuery != null) {
            chatId = update.CallbackQuery.Message.Chat.Id;
        } else
        { 
            return update;
        }

        Dictionaries.UserState[chatId] = States.Start;
        await botClient.DeleteMessage(chatId, update.Message.MessageId, cancellationToken: cancellationToken);
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📄 Новый рецепт", "add_recipe"),
                InlineKeyboardButton.WithCallbackData("⚙️ Настройки", "confirm_no")
            }
        });
        await botClient.SendMessage(
            chatId: chatId,
            text: "Выберите действие:",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken
        );

        return update;
    }
}
