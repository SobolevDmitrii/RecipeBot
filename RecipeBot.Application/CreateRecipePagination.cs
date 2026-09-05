using MyRecipeDomain.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace RecipeBot.Application;

public class CreateRecipePagination
{
    public static InlineKeyboardMarkup CreatePagination(
        IReadOnlyList<Recipe> recipes,
        int page,
        int pageSize)
    {
        var totalPages = (int)Math.Ceiling(recipes.Count / (double)pageSize);

        var pageRecipes = recipes
            .Skip(page * pageSize)
            .Take(pageSize);

        var buttons = pageRecipes
            .Select(recipe => new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    $"📖 {recipe.Name}",
                    $"recipe:{recipe.Id}")
            })
            .ToList();

        var navigation = new List<InlineKeyboardButton>();

        if (page > 0)
        {
            navigation.Add(
                InlineKeyboardButton.WithCallbackData(
                    "◀️",
                    $"recipes:page:{page - 1}"));
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
                    $"recipes:page:{page + 1}"));
        }

        buttons.Add(navigation.ToArray());

        buttons.Add(
        [
            InlineKeyboardButton.WithCallbackData("↩️ Назад", "cancel")
        ]);

        return new InlineKeyboardMarkup(buttons);
    }
}