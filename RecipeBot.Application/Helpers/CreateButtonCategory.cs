using RecipeBot.Application.Models;
using RecipeBot.Domain.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace RecipeBot.Application.Helpers;

public class CreateButtonCategory
{
    public static InlineKeyboardMarkup CreateButtonCategories(IReadOnlyList<CategoryDto> categories)
    {
        var buttons = categories
            .Select(category => InlineKeyboardButton.WithCallbackData($"{category.Name}", $"{category.Id}"))
            .Chunk(3)
            .ToArray();

        return new InlineKeyboardMarkup(buttons);
    }
}
