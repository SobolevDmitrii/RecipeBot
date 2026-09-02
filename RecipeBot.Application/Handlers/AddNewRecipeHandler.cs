using MediatR;
using MyRecipeDomain.Models;
using RecipeBot.Domain.Abstract.IRepositories;
using RecipeBot.Domain.Enums;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace RecipeBot.Application.Handlers;

public class AddNewRecipeRequest(Update update) : IRequest
{
    public Update Update { get; set; } = update;
}

public class AddNewRecipeHandler(
    ITelegramBotClient botClient,
    IMediator mediator,
    IRecipeRepository repository)
    : IRequestHandler<AddNewRecipeRequest>
{
    public async Task Handle(AddNewRecipeRequest request, CancellationToken cancellationToken)
    {
        var update = request.Update;

        if (!string.IsNullOrWhiteSpace(update.Message?.Text))
        {
            var chatId = update.Message.Chat.Id;

            if (Dictionaries.RecipeState.TryGetValue(chatId, out var state))
            {
                switch (state)
                {
                    case States.WaitingForRecipeName:
                        await AddNameRecipe(update, chatId, cancellationToken);
                        break;
                    case States.WaitingForIngredients:
                        await AddIngredients(update, chatId, cancellationToken);
                        break;
                    case States.AddingStage:
                        await AddStage(update, chatId, cancellationToken);
                        break;
                    default:
                        throw new InvalidDataException();
                }
            }
        }

        if (update.CallbackQuery != null)
        {
            var chatId = update.CallbackQuery.Message.Chat.Id;
            switch (update.CallbackQuery.Data)
            {
                case "add_image":
                case "add_step":
                    Dictionaries.RecipeState[chatId] = States.AddingStage;
                    await WaitingDescription(update, chatId, cancellationToken);
                    break;
                case "cancel":
                    await CancelRecipe(update, cancellationToken);
                    break;
                case "done":
                    await Done(update, chatId, cancellationToken);
                    break;
                default:
                    break;
            }
        }
    }

    private async Task AddNameRecipe(Update update, long chatId, CancellationToken cancellationToken)
    {
        Dictionaries.RecipeDrafts[chatId] = new Recipe
        {
            Name = update.Message?.Text
        };
        //await botClient.DeleteMessage(chatId, update.Message.MessageId, cancellationToken: cancellationToken);
        await botClient.SendTextMessageAsync(
            chatId,
            "✏️ Введите ингредиенты:",
            cancellationToken: cancellationToken
        );
        Dictionaries.RecipeState[chatId] = States.WaitingForIngredients;
    }

    private async Task AddIngredients(Update update, long chatId, CancellationToken cancellationToken)
    {
        if (Dictionaries.RecipeDrafts.TryGetValue(chatId, out var recipe))
        {
            recipe.Ingredients = update.Message?.Text;
            //await botClient.DeleteMessage(chatId, update.Message.MessageId, cancellationToken: cancellationToken);
            await botClient.SendTextMessageAsync(chatId, "✅ Выберите один из вариантов.",
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("🖼️ Добавить фотографию блюда", "add_image"),
                        InlineKeyboardButton.WithCallbackData("📝 Добавить этап приготовления", "add_step"),
                        InlineKeyboardButton.WithCallbackData("❌ Отменить добавление рецепта", "cancel")
                    }
                }), cancellationToken: cancellationToken);
            Dictionaries.RecipeState[chatId] = States.WaitingMove;
        }
        else
        {
            await botClient.EditMessageReplyMarkup(chatId, update.Message.MessageId, new InlineKeyboardMarkup(),
                cancellationToken: cancellationToken);
            await botClient.SendMessage(chatId, "⚠️ Вы ещё не ввели текст.", cancellationToken: cancellationToken);
        }
    }

    private async Task CancelRecipe(Update update, CancellationToken cancellationToken)
    {
        await mediator.Send(new StartMenuRequest(update), cancellationToken);
    }

    private async Task AddStage(Update update, long chatId, CancellationToken cancellationToken)
    {
        if (Dictionaries.RecipeDrafts.TryGetValue(chatId, out var recipe))
        {
            if (Dictionaries.RecipeState.TryGetValue(chatId, out var states))
            {
                var number = recipe.Stages == null ? 1 : recipe.Stages.Count + 1;

                recipe.Stages ??= new List<Stage>();
                recipe.Stages.Add(
                    new Stage
                    {
                        Description = update.Message?.Text,
                        Number = number
                    }
                );
            }
        }

        await botClient.SendTextMessageAsync(chatId, "✅ Выберите один из вариантов.",
            replyMarkup: new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🖼️ Добавить фотографию этапа", "add_image_stage"),
                    InlineKeyboardButton.WithCallbackData("📝 Следующий этап приготовления", "add_step"),
                    InlineKeyboardButton.WithCallbackData("📝 Завершить", "done"),
                    InlineKeyboardButton.WithCallbackData("❌ Отменить добавление рецепта", "cancel")
                }
            }), cancellationToken: cancellationToken);
    }

    private async Task WaitingDescription(Update update, long chatId, CancellationToken cancellationToken)
    {
        await botClient.DeleteMessage(chatId, update.CallbackQuery.Message.MessageId);
        await botClient.SendTextMessageAsync(
            chatId,
            "✏️ Введите описание этапа:",
            cancellationToken: cancellationToken
        );
    }

    private async Task Done(Update update, long chatId, CancellationToken cancellationToken)
    {
        if (Dictionaries.RecipeDrafts.TryGetValue(chatId, out var recipe))
        {
            await repository.Add(recipe, chatId, cancellationToken);
        }

        await mediator.Send(new StartMenuRequest(update), cancellationToken);
    }
}