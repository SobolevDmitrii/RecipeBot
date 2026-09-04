using Telegram.Bot.Types.ReplyMarkups;

namespace RecipeBot.Application.Models;

public record HandlerResult
(
    string Message,
    InlineKeyboardMarkup? Buttons);