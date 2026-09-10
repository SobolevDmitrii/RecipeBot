using MediatR;
using RecipeBot.Application.Abstract;
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

public class StartMenuHandler(IUserSessionStore userSessionStore) : IRequestHandler<StartMenuRequest,HandlerResult?>
{
    public async Task<HandlerResult?> Handle(StartMenuRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text) && string.IsNullOrWhiteSpace(request.CallBack))
        {
            return null;
        }

        var session = await userSessionStore.GetOrAddUserSession(request.ChatId);
        session.UserState = UserStates.Start;
        await userSessionStore.SetUserSession(request.ChatId, session);

        var buttons = new[]
        {
            InlineKeyboardButton.WithCallbackData("📄 Новый рецепт", "add_recipe"),
            InlineKeyboardButton.WithCallbackData("📄 Мои рецепты", "my_recipe"),
           // InlineKeyboardButton.WithCallbackData("⚙️ Настройки", "confirm_no")
        };
        return new HandlerResult(@"Если вы захотите отменить действие на любом этапе просто введите команду '/cancel'
Выберите действие:", null,new InlineKeyboardMarkup(buttons));
    }
}
