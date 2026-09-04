using MediatR;
using MyRecipeDomain.Models;
using RecipeBot.Application.Models;
using RecipeBot.Domain.Abstract.IRepositories;
using RecipeBot.Domain.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace RecipeBot.Application.Handlers;

public record AddNewRecipeRequest(
    long ChatId,
    string? Text,
    string? ImageFileId,
    string? CallbackData,
    string? Caption) : IRequest<HandlerResult?>;


public class AddNewRecipeHandler(
    IMediator mediator,
    IRecipeRepository recipeRepository,
    ICategoryRepository categoryRepository)
    : IRequestHandler<AddNewRecipeRequest,HandlerResult?>
{
    public async Task<HandlerResult?> Handle(AddNewRecipeRequest request, CancellationToken cancellationToken)
    {
        //var update = request.Update;
        var chatId = request.ChatId;
        if (!string.IsNullOrWhiteSpace(request.Text) || !string.IsNullOrWhiteSpace(request.Caption))
        { if (Dictionaries.UserSessions.TryGetValue(chatId, out var session))
            {
                switch (session.RecipeState)
                {
                    case RecipeStates.WaitingForRecipeName:
                        return await AddNameRecipe(request.Text, chatId, cancellationToken);
                    case RecipeStates.WaitingForImageRecipe:
                        return await AddImageRecipe(request.ImageFileId, chatId);
                    case RecipeStates.WaitingForIngredients:
                        return await AddIngredients(request.Text, chatId); 
                    case RecipeStates.AddingStage:
                        return await AddStage(request.Text,request.Caption,request.ImageFileId, chatId);
                    default:
                        throw new InvalidDataException();
                }
            }
        }

        if (request.CallbackData != null)
        {
            switch (request.CallbackData)
            {
                case "add_image":
                    return await WaitingImageRecipe(chatId);
                case "add_step":
                    return await WaitingDescription(chatId);
                case "cancel":
                    return await CancelRecipe(request.ChatId,request.Text,request.CallbackData, cancellationToken);
                case "done":
                    return await Done(request.ChatId,request.Text,request.CallbackData, cancellationToken);
            }

            if (Dictionaries.UserSessions.TryGetValue(chatId, out var session))
            {
                switch (session.RecipeState)
                {
                    case RecipeStates.WaitingForCategory:
                        return await AddCategory(request.CallbackData,chatId);
                }
            }
        }
        return null;
    }

    private async Task<HandlerResult> WaitingImageRecipe(long chatId)
    {
        
        Dictionaries.UserSessions[chatId].RecipeState = RecipeStates.WaitingForImageRecipe;
        return new HandlerResult("Загрузите и отправте картинку в чат.", null);
    }

    private async Task<HandlerResult?> AddImageRecipe(string imageFileId, long chatId)
    {
        if (Dictionaries.UserSessions.TryGetValue(chatId, out var session))
        {
            session.RecipeDraft.SetUrlImage(imageFileId);
            var buttons = new[]
            {
                InlineKeyboardButton.WithCallbackData("🖼️ Изменить фотографию блюда", "add_image"),
                InlineKeyboardButton.WithCallbackData("📝 Добавить этап приготовления", "add_step"),
                InlineKeyboardButton.WithCallbackData("❌ Отменить добавление рецепта", "cancel")
            };
            Dictionaries.UserSessions[chatId].RecipeState = RecipeStates.WaitingMove;
            return new HandlerResult("✅ Выберите один из вариантов.", new InlineKeyboardMarkup(buttons));
        }

        return null;
    }

    private async Task<HandlerResult> AddNameRecipe(string text, long chatId, CancellationToken cancellationToken)
    {
        var recipe = new Recipe();
        recipe.SetName(text);
        Dictionaries.UserSessions[chatId].RecipeDraft = recipe;
        var categories = await categoryRepository.GetAllCategories(cancellationToken);
        var buttons = new List<InlineKeyboardButton>();
        foreach (var category in categories)
        {
            buttons.Add(InlineKeyboardButton.WithCallbackData($"{category.Name}", $"{category.Id}"));
        }

        /*await botClient.SendMessage(chatId, "✅ Выберите категорию.",
            replyMarkup: new InlineKeyboardMarkup(new[]
            {
                buttons
            }), cancellationToken: cancellationToken);*/
        Dictionaries.UserSessions[chatId].RecipeState = RecipeStates.WaitingForCategory;
        return new HandlerResult("✅ Выберите категорию.", new InlineKeyboardMarkup(buttons));
    }

    private async Task<HandlerResult?> AddCategory(string callBack,  long chatId)
    {
        var categoryId = int.Parse(callBack);
        if (Dictionaries.UserSessions.TryGetValue(chatId, out var session))
        {
            session.RecipeDraft.SetCategoryId(categoryId);
            Dictionaries.UserSessions[chatId].RecipeState = RecipeStates.WaitingForIngredients;
            return new HandlerResult("✏️ Введите ингредиенты:", null);
        }

        return null;
    }

    private async Task<HandlerResult?> AddIngredients(string text, long chatId)
    {
        if (Dictionaries.UserSessions.TryGetValue(chatId, out var session))
        {
            session.RecipeDraft.SetIngredients(text);
            
            Dictionaries.UserSessions[chatId].RecipeState = RecipeStates.WaitingMove;
            var buttons = new[]
            {
                InlineKeyboardButton.WithCallbackData("🖼️ Добавить фотографию блюда", "add_image"),
                InlineKeyboardButton.WithCallbackData("📝 Добавить этап приготовления", "add_step"),
                InlineKeyboardButton.WithCallbackData("❌ Отменить добавление рецепта", "cancel")
            };
            return new HandlerResult("✅ Выберите один из вариантов.", new InlineKeyboardMarkup(buttons));
        }
        return null;
    }

    private async Task<HandlerResult?> CancelRecipe(long chatId,string? text, string? callBack, CancellationToken cancellationToken)
    {
        return await mediator.Send(new StartMenuRequest(chatId, text, callBack), cancellationToken);
    }

    private async Task<HandlerResult> AddStage(string? text, string? caption, string? imageFileId, long chatId)
    {
        if (Dictionaries.UserSessions.TryGetValue(chatId, out var session))
        {
            var number = session.RecipeDraft.Stages == null ? 1 : session.RecipeDraft.Stages.Count + 1;
            var stage = new Stage();

            stage.SetDescription(text ??= caption);
            stage.SetNumber(number);
            
            if (!string.IsNullOrWhiteSpace(imageFileId))
            {
                stage.SetUrlImage(imageFileId);
            }


            session.RecipeDraft.AddStage(stage);
        }
        
        var buttons = new[]
        {
            InlineKeyboardButton.WithCallbackData("📝 Следующий этап приготовления", "add_step"),
            InlineKeyboardButton.WithCallbackData("📝 Завершить", "done"),
            InlineKeyboardButton.WithCallbackData("❌ Отменить добавление рецепта", "cancel")
        };
        return new HandlerResult("✅ Выберите один из вариантов.", new InlineKeyboardMarkup(buttons));
    }

    private async Task<HandlerResult> WaitingDescription(long chatId)
    {
        Dictionaries.UserSessions[chatId].RecipeState = RecipeStates.AddingStage;
        return new HandlerResult("✏️ Введите описание этапа, если хотите добавить картинку этапа, просто пришлите ее вместе с текстом:", null);
    }

    private async Task<HandlerResult?> Done(long chatId,string? text, string? callBack, CancellationToken cancellationToken)
    {
        if (Dictionaries.UserSessions.TryGetValue(chatId, out var session))
        {
            await recipeRepository.Add(session.RecipeDraft, chatId, cancellationToken);
        }

        return await mediator.Send(new StartMenuRequest(chatId, text, callBack), cancellationToken);
    }
}