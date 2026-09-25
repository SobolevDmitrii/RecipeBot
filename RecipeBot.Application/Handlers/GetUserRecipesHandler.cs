using MediatR;
using RecipeBot.Application.Abstract;
using RecipeBot.Application.Helpers;
using RecipeBot.Application.Mappers.Extensions;
using RecipeBot.Application.Models;
using RecipeBot.Domain.Abstract.IRepositories;
using RecipeBot.Domain.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace RecipeBot.Application.Handlers;

public record GetUserRecipesRequest(
    long ChatId,
    string? text,
    string? CallbackData,
    int UpdateId) : IRequest<HandlerResult?>;

public class GetUserRecipesHandler(
    IRecipeRepository recipeRepository,
    ICategoryRepository categoryRepository,
    IUserSessionStore userSessionStore) : IRequestHandler<GetUserRecipesRequest, HandlerResult?>
{
    public async Task<HandlerResult?> Handle(GetUserRecipesRequest request, CancellationToken cancellationToken)
    {
        var chatId = request.ChatId;
        var session = await userSessionStore.TryGetSession(request.ChatId);
        if (session == null)
            return new HandlerResult("Сессия истекла. Отправьте /start и выберите рецепт снова.", null, null);
        
        session.ChatId = chatId;
        session.CurrentUpdateId = request.UpdateId;
        await userSessionStore.AddProcessing(chatId);
        await userSessionStore.SetUserSession(chatId, session);

        var result = await HandleAdd(request, session, cancellationToken);
        await userSessionStore.SetUserSession(request.ChatId, session);
        return result;
    }
    
    public async Task<HandlerResult?> HandleAdd(GetUserRecipesRequest request, UserSession userSession, CancellationToken cancellationToken)
    {
        var chatId = request.ChatId;
        switch (userSession.RecipeState)
        {
            case RecipeStates.ShowRecipeSelectionOptions:
                return await ShowRecipeSelectionOptions(chatId,userSession);
            case RecipeStates.ShowRecipes:
                return await HandleRecipeSelection(
                    chatId,
                    request.CallbackData,
                    userSession);
            case RecipeStates.WaitingForCategory:
                return await SearchByCategory(chatId, request.CallbackData,userSession, cancellationToken);
            case RecipeStates.WaitingForRecipeName:
                return await SearchByName(chatId, request.text,userSession, cancellationToken);
            case RecipeStates.WaitMoveForRecipe:
                return await GetMoveForRecipe(chatId, request.CallbackData,userSession, cancellationToken);
        }


        if (request.CallbackData != null)
        {
            switch (request.CallbackData)
            {
                case "all_list":
                    return await GetAllRecipe(chatId,userSession, cancellationToken);
                case "for_category":
                    return await GetRecipesByCateGory(chatId,userSession, cancellationToken);
                case "search_by_name":
                    return await WaitingNameForSearch(request.ChatId,userSession);
                case "cancel":
                    return await CancelRecipe(request.ChatId,userSession);
            }
        }

        return null;
    }

    private async Task<HandlerResult?> GetMoveForRecipe(long chatId, string callbackData,
        UserSession userSession,
        CancellationToken cancellationToken)
    {
        if (callbackData.StartsWith("recipe:view:"))
        {
            var recipeId = int.Parse(
                callbackData["recipe:view:".Length..]);
            return await ViewRecipe(chatId, recipeId,userSession, cancellationToken);
        }

        if (callbackData.StartsWith("recipe:delete:"))
        {
            var recipeId = int.Parse(
                callbackData["recipe:delete:".Length..]);
            return await RemoveRecipe(chatId, recipeId,userSession, cancellationToken);
        }

        if (callbackData.StartsWith("recipe:stages:"))
        {
            var stage = int.Parse(
                callbackData["recipe:stages:".Length..]);
            return await ViewStageRecipe(stage,userSession);
        }

        if (callbackData == "recipe:back")
        {
            userSession.RecipeState = RecipeStates.ShowRecipeSelectionOptions;
            await userSessionStore.SetUserSession(chatId,userSession);
            return await ShowRecipeSelectionOptions(chatId,userSession);
        }

        return null;
    }

    private async Task<HandlerResult?> ViewStageRecipe(int stage, UserSession userSession)
    {
        var recipe = userSession.CurrentViewRecipe;
        if (recipe == null)
        {
            return null;
        }

        return CreateStagePagination.CreatePagination(recipe, stage);
    }

    private async Task<HandlerResult?> ViewRecipe(long chatId, int recipeId, UserSession userSession,
        CancellationToken cancellationToken)
    {
        var recipe = await recipeRepository.Get(chatId, recipeId, cancellationToken);
       
        userSession.CurrentViewRecipe = recipe.ToDto();
        var buttons = new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "👁️ Просмотреть этапы",
                    $"recipe:stages:0")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "🗑️ Удалить",
                    $"recipe:delete:{recipe.Id}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "↩️ Назад",
                    "recipe:back")
            }
        };

        var message = $"""
                       🍲 {recipe.Name}

                       📂 Категория: {recipe.Category.Name}

                       🥕 Ингредиенты:
                       {recipe.Ingredients}

                       👨‍🍳 Этапов приготовления: {recipe.Stages.Count}
                       """;

        await userSessionStore.SetUserSession(chatId, userSession);
        return new HandlerResult(
            message,
            recipe.UrlImage,
            new InlineKeyboardMarkup(buttons));
    }

    private async Task<HandlerResult?> RemoveRecipe(long chatId, int recipeId, UserSession userSession,
        CancellationToken cancellationToken)
    {
        await recipeRepository.Delete(chatId, recipeId, cancellationToken);
        userSession.RecipeState = RecipeStates.ShowRecipeSelectionOptions;
        await userSessionStore.SetUserSession(chatId, userSession);
        return await ShowRecipeSelectionOptions(chatId, userSession);
    }

    private async Task<HandlerResult> WaitingNameForSearch(long chatId, UserSession userSession)
    {
        userSession.RecipeState = RecipeStates.WaitingForRecipeName;
        await userSessionStore.SetUserSession(chatId, userSession);
        return new HandlerResult("Введите полное и частичное наименование рецепта.", null, null);
    }

    private async Task<HandlerResult?> SearchByName(long chatId, string text, UserSession userSession,
        CancellationToken cancellationToken)
    {
        var recipes = (await recipeRepository.SearchByName(chatId, text, cancellationToken)).ToDto();

        var buttons = CreateRecipePagination.CreatePagination(
            recipes,
            page: 0,
            5);
        userSession.RecipeState = RecipeStates.ShowRecipes;
        userSession.Recipes = recipes;
        await userSessionStore.SetUserSession(chatId, userSession);
        return new HandlerResult("Выбери рецепт", null, buttons);
    }

    private async Task<HandlerResult?> SearchByCategory(long chatId, string callbackData, UserSession userSession,
        CancellationToken cancellationToken)
    {
        var recipes =
            (await recipeRepository.GetListByTelegramIdAndCategory(chatId, int.Parse(callbackData), cancellationToken))
            .ToDto();

        var buttons = CreateRecipePagination.CreatePagination(
            recipes,
            page: 0,
            5);
        userSession.RecipeState = RecipeStates.ShowRecipes;
        userSession.Recipes = recipes;
        await userSessionStore.SetUserSession(chatId, userSession);
        return new HandlerResult("Выбери рецепт", null, buttons);
    }

    private async Task<HandlerResult?> GetRecipesByCateGory(long chatId, UserSession userSession,
        CancellationToken cancellationToken)
    {
        var categories = await categoryRepository.GetAllCategories(cancellationToken);
        var buttons = new List<InlineKeyboardButton>();
        foreach (var category in categories)
        {
            buttons.Add(InlineKeyboardButton.WithCallbackData($"{category.Name}", $"{category.Id}"));
        }

        userSession.RecipeState = RecipeStates.WaitingForCategory;
        await userSessionStore.SetUserSession(chatId, userSession);
        return new HandlerResult("✅ Выберите категорию.", null, new InlineKeyboardMarkup(buttons));
    }

    private HandlerResult? GetPageRecipe(string callbackData, UserSession userSession)
    {
        if (callbackData.StartsWith("recipes:page:"))
        {
            var page = int.Parse(
                callbackData["recipes:page:".Length..]);

            return new HandlerResult(
                "📋 Ваши рецепты:",
                null,
                CreateRecipePagination
                    .CreatePagination(userSession.Recipes, page, 5));
        }

        return null;
    }

    private async Task<HandlerResult?> CancelRecipe(long chatId, UserSession userSession)
    {
        userSession.UserState = UserStates.Cansel;
        await userSessionStore.SetUserSession(chatId, userSession);
        return null;
    }

    private async Task<HandlerResult?> HandleRecipeSelection(
        long chatId,
        string? callbackData,
        UserSession userSession)
    {
        if (string.IsNullOrWhiteSpace(callbackData))
            return null;

        if (callbackData.StartsWith("recipes:page:"))
        {
            return GetPageRecipe(callbackData, userSession);
        }

        if (callbackData.StartsWith("recipe:"))
        {
            var recipeIdText = callbackData["recipe:".Length..];

            if (!int.TryParse(recipeIdText, out var recipeId))
                return null;

            return await GetRecipeVaribles(
                chatId,
                recipeId,
                userSession);
        }

        return null;
    }

    private async Task<HandlerResult> GetRecipeVaribles(long chatId, int recipeId, UserSession userSession)
    {
        var buttons = new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "👁️ Просмотреть",
                    $"recipe:view:{recipeId}"),
                
                InlineKeyboardButton.WithCallbackData(
                    "✏️ Редактировать",
                    $"recipe:edit:{recipeId}"),

                InlineKeyboardButton.WithCallbackData(
                    "🗑️ Удалить",
                    $"recipe:delete:{recipeId}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "↩️ Назад",
                    "recipe:back")
            }
        };
        userSession.RecipeState = RecipeStates.WaitMoveForRecipe;
        await userSessionStore.SetUserSession(chatId, userSession);
        return new HandlerResult("Выбери действие", null, buttons);
    }

    private async Task<HandlerResult?> GetAllRecipe(long chatId, UserSession userSession,
        CancellationToken cancellationToken)
    {
        var recipes = (await recipeRepository.GetListByTelegramId(chatId, cancellationToken)).ToDto();

        var buttons = CreateRecipePagination.CreatePagination(
            recipes,
            page: 0,
            5);
        userSession.RecipeState = RecipeStates.ShowRecipes;
        userSession.Recipes = recipes;
        await userSessionStore.SetUserSession(chatId, userSession);

        return new HandlerResult("Выбери рецепт", null, buttons);
    }

    private async Task<HandlerResult?> ShowRecipeSelectionOptions(long chatId, UserSession userSession)
    {
        var buttons = new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📋 Весь список", "all_list"),
                InlineKeyboardButton.WithCallbackData("📂 По категории", "for_category")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔎 Поиск по имени", "search_by_name")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("↩️ Назад", "cancel")
            }
        };
        userSession.RecipeState = RecipeStates.WaitingMove;
        await userSessionStore.SetUserSession(chatId, userSession);
        return new HandlerResult("✅ Выберите один из вариантов.", null, new InlineKeyboardMarkup(buttons));
    }

   
}