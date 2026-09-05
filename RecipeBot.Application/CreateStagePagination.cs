using MyRecipeDomain.Models;
using RecipeBot.Application.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace RecipeBot.Application;

public class CreateStagePagination
{
    public static HandlerResult CreatePagination(
        Recipe recipe,
        int page)
    {
        var totalPages = recipe.Stages.Count;
        
        var message = $"""
                       👨‍🍳 {recipe.Name} — этап {page + 1} / {totalPages}

                       {recipe.Stages[page].Description}
                       """;
        
        
        var navigation = new List<InlineKeyboardButton>();

        if (page > 0)
        {
            navigation.Add(
                InlineKeyboardButton.WithCallbackData(
                    "◀️",
                    $"recipe:stages:{page - 1}"));
        }

        navigation.Add(
            InlineKeyboardButton.WithCallbackData(
                $"{page + 1} / {totalPages}",
                "noop"));

        if (page < totalPages - 1)
        {
            navigation.Add(
                InlineKeyboardButton.WithCallbackData(
                    "▶️",
                    $"recipe:stages:{page + 1}"));
        }

        var buttons = new List<InlineKeyboardButton[]>();
        buttons.Add(navigation.ToArray());

        buttons.Add(
        [
            InlineKeyboardButton.WithCallbackData("↩️ Назад", "cancel")
        ]);
        
        
        return new HandlerResult(
            message,
            new InlineKeyboardMarkup(buttons));
    }
}