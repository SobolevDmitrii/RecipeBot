using MediatR;
using RecipeBot.Application.Abstract;
using RecipeBot.Application.Mappers.Extensions;
using RecipeBot.Application.Models;
using RecipeBot.Domain.Abstract.IRepositories;
using RecipeBot.Domain.Enums;
using RecipeBot.Domain.Models;
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
    ICategoryRepository categoryRepository,
    IUserSessionStore userSessionStore)
    : IRequestHandler<AddNewRecipeRequest,HandlerResult?>
{
    public async Task<HandlerResult?> Handle(AddNewRecipeRequest request, CancellationToken cancellationToken)
    {
        var chatId = request.ChatId;
        var userSession = await userSessionStore.TryGetSession(request.ChatId);
        if (!string.IsNullOrWhiteSpace(request.Text) || !string.IsNullOrWhiteSpace(request.Caption )
            || !string.IsNullOrWhiteSpace(request.ImageFileId ))
        {
            if (userSession != null)
            {
                switch (userSession.RecipeState)
                {
                    case RecipeStates.WaitingForRecipeName:
                        return await AddNameRecipe(request.Text, chatId,userSession, cancellationToken);
                    case RecipeStates.WaitingForImageRecipe:
                        return await AddImageRecipe(request.ImageFileId, chatId,userSession);
                    case RecipeStates.WaitingForIngredients:
                        return await AddIngredients(request.Text, chatId,userSession); 
                    case RecipeStates.AddingStage:
                        return await AddStage(request.Text,request.Caption,request.ImageFileId, chatId,userSession);
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
                    return await WaitingImageRecipe(chatId,userSession);
                case "add_step":
                    return await WaitingDescription(chatId,userSession);
                case "cancel":
                    return await CancelRecipe(request.ChatId,userSession);
                case "done":
                    return await Done(request.ChatId,request.Text,request.CallbackData,userSession, cancellationToken);
            }

            if (userSession != null)
            {
                switch (userSession.RecipeState)
                {
                    case RecipeStates.WaitingForCategory:
                        return await AddCategory(request.CallbackData,chatId,userSession);
                }
            }
        }
        return null;
    }

    private async Task<HandlerResult> WaitingImageRecipe(long chatId , UserSession? userSession)
    {
        if (userSession == null)
            return new HandlerResult("Произошла непредвиденная ошибка, повторите снова", null, null);
        userSession.RecipeState = RecipeStates.WaitingForImageRecipe;
        await userSessionStore.SetUserSession(chatId, userSession);
        return new HandlerResult("Загрузите и отправте картинку в чат.", null,null);
    }

    private async Task<HandlerResult?> AddImageRecipe(string imageFileId, long chatId, UserSession? userSession)
    {
        if (userSession !=null)
        {
            userSession.RecipeDraft.UrlImage  = imageFileId;
            var buttons = new[]
            {
                InlineKeyboardButton.WithCallbackData("🖼️ Изменить фотографию блюда", "add_image"),
                InlineKeyboardButton.WithCallbackData("📝 Добавить этап приготовления", "add_step"),
                InlineKeyboardButton.WithCallbackData("❌ Отменить добавление рецепта", "cancel")
            };
            userSession.RecipeState = RecipeStates.WaitingMove;
            await userSessionStore.SetUserSession(chatId, userSession);
            return new HandlerResult("✅ Выберите один из вариантов.", null,new InlineKeyboardMarkup(buttons));
        }

        return null;
    }

    private async Task<HandlerResult> AddNameRecipe(string text, long chatId, UserSession? userSession, CancellationToken cancellationToken)
    {
        if (userSession == null)
            return new HandlerResult("Произошла непредвиденная ошибка, повторите снова", null, null);
        var recipe = new Recipe();
        recipe.SetName(text);
        userSession.RecipeDraft = recipe.ToDto();
        var categories = await categoryRepository.GetAllCategories(cancellationToken);
        var buttons = new List<InlineKeyboardButton>();
        foreach (var category in categories)
        {
            buttons.Add(InlineKeyboardButton.WithCallbackData($"{category.Name}", $"{category.Id}"));
        }
        userSession.RecipeState = RecipeStates.WaitingForCategory;
        await userSessionStore.SetUserSession(chatId, userSession);
        return new HandlerResult("✅ Выберите категорию.", null,new InlineKeyboardMarkup(buttons));
    }

    private async Task<HandlerResult?> AddCategory(string callBack,  long chatId , UserSession? userSession)
    {
        var categoryId = int.Parse(callBack);
        if (userSession!=null)
        {
            userSession.RecipeDraft.CategoryId = categoryId;
            userSession.RecipeState = RecipeStates.WaitingForIngredients;
            await userSessionStore.SetUserSession(chatId, userSession);
            return new HandlerResult("✏️ Введите ингредиенты:", null,null);
        }
        return null;
    }

    private async Task<HandlerResult?> AddIngredients(string text, long chatId, UserSession? userSession)
    {
        if (userSession!=null)
        {
            userSession.RecipeDraft.Ingredients = text;
            
            userSession.RecipeState = RecipeStates.WaitingMove;
            var buttons = new[]
            {
                InlineKeyboardButton.WithCallbackData("🖼️ Добавить фотографию блюда", "add_image"),
                InlineKeyboardButton.WithCallbackData("📝 Добавить этап приготовления", "add_step"),
                InlineKeyboardButton.WithCallbackData("❌ Отменить добавление рецепта", "cancel")
            };
            await userSessionStore.SetUserSession(chatId, userSession);
            return new HandlerResult("✅ Выберите один из вариантов.", null,new InlineKeyboardMarkup(buttons));
        }
        return null;
    }

    private async Task<HandlerResult?> CancelRecipe(long chatId, UserSession? userSession)
    {
        if (userSession == null)
            return new HandlerResult("Произошла непредвиденная ошибка, повторите снова", null, null);
        userSession.UserState = UserStates.Cansel;
        await userSessionStore.SetUserSession(chatId, userSession);
        return null;
    }

    private async Task<HandlerResult> AddStage(string? text, string? caption, string? imageFileId, long chatId, UserSession? userSession)
    {
        if (userSession !=null)
        {
            var number = userSession.RecipeDraft.Stages == null ? 1 : userSession.RecipeDraft.Stages.Count + 1;
            var stage = new Stage();

            stage.SetDescription(text ??= caption);
            stage.SetNumber(number);
            
            if (!string.IsNullOrWhiteSpace(imageFileId))
            {
                stage.SetUrlImage(imageFileId);
            }


            userSession.RecipeDraft.Stages.Add(stage.ToDto());
        }
        
        var buttons = new[]
        {
            InlineKeyboardButton.WithCallbackData("📝 Следующий этап приготовления", "add_step"),
            InlineKeyboardButton.WithCallbackData("📝 Завершить", "done"),
            InlineKeyboardButton.WithCallbackData("❌ Отменить добавление рецепта", "cancel")
        };
        await userSessionStore.SetUserSession(chatId, userSession);
        return new HandlerResult("✅ Выберите один из вариантов.", null,new InlineKeyboardMarkup(buttons));
    }

    private async Task<HandlerResult> WaitingDescription(long chatId,  UserSession? userSession)
    {
        if (userSession == null)
            return new HandlerResult("Произошла непредвиденная ошибка, повторите снова", null, null);
        userSession.RecipeState = RecipeStates.AddingStage;
        await userSessionStore.SetUserSession(chatId, userSession);
        return new HandlerResult("✏️ Введите описание этапа, если хотите добавить картинку этапа, просто пришлите ее вместе с текстом:", null,null);
    }

    private async Task<HandlerResult?> Done(long chatId,string? text, string? callBack, UserSession? userSession, CancellationToken cancellationToken)
    {
        if (userSession != null)
        {
            await recipeRepository.Add(userSession.RecipeDraft.ToEntity(), chatId, cancellationToken);
        }

        return await mediator.Send(new StartMenuRequest(chatId, text, callBack), cancellationToken);
    }
}