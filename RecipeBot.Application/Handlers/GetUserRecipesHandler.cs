using MediatR;
using RecipeBot.Application.Models;
using RecipeBot.Domain.Abstract.IRepositories;
using RecipeBot.Domain.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace RecipeBot.Application.Handlers;


public record GetUserRecipesRequest(
long ChatId,
string? text,
string? CallbackData) : IRequest<HandlerResult?>;

public class GetUserRecipesHandler(IRecipeRepository recipeRepository ,
    ICategoryRepository categoryRepository) : IRequestHandler<GetUserRecipesRequest, HandlerResult?>
{
    public async Task<HandlerResult?> Handle(GetUserRecipesRequest request, CancellationToken cancellationToken)
    {
        var chatId = request.ChatId;
        if (UserSessionsStore.UserSessions.TryGetValue(chatId, out var session))
        {
            switch (session.RecipeState)
            {
                case RecipeStates.ShowRecipeSelectionOptions:
                    return ShowRecipeSelectionOptions(chatId);
                case RecipeStates.ShowRecipces:
                    return await HandleRecipeSelection(
                        chatId,
                        request.CallbackData,
                        cancellationToken);
                case RecipeStates.WaitingForCategory:
                    return await SearchByCategory(chatId,request.CallbackData,cancellationToken);
                case RecipeStates.WaitingForRecipeName:
                    return await SearchByName(chatId,request.text,cancellationToken);
                case RecipeStates.WaitMoveForRecipe:
                    return await GetMoveForRecipe(chatId,request.CallbackData,cancellationToken);
                
            }
        }
        if (request.CallbackData != null)
        {
            switch (request.CallbackData)
            {
                case "all_list":
                    return await GetAllRecipe(chatId,cancellationToken);
                case "for_category":
                    return await GetRecipesByCateGory(chatId,cancellationToken);
                case "search_by_name":
                    return  WaitingNameForSearch(request.ChatId);
                case "cancel":
                    return  CancelRecipe(request.ChatId);
                
            }
        }
        return null;
    }

    private async Task<HandlerResult?> GetMoveForRecipe(long chatId, string callbackData, CancellationToken cancellationToken)
    {
        if (callbackData.StartsWith("recipe:view:"))
        {
            var recipeId = int.Parse(
                callbackData["recipe:view:".Length..]);
            return await ViewRecipe(chatId, recipeId, cancellationToken);
        }

        if (callbackData.StartsWith("recipe:delete:"))
        {
            var recipeId = int.Parse(
                callbackData["recipe:delete:".Length..]);
            return await RemoveRecipe(chatId, recipeId, cancellationToken);
        }
        
        if (callbackData.StartsWith("recipe:stages:"))
        {
            var stage = int.Parse(
                callbackData["recipe:stages:".Length..]);
            return await ViewStageRecipe(chatId, stage);
        }

        if (callbackData == "recipe:back")
        {
            UserSessionsStore.UserSessions[chatId].RecipeState = RecipeStates.ShowRecipeSelectionOptions;
            return ShowRecipeSelectionOptions(chatId);
        }

        return null;
    }

    private async Task<HandlerResult?> ViewStageRecipe(long chatId, int stage)
    {
        var recipe = UserSessionsStore.UserSessions[chatId].CurrentViewRecipe;
        if (recipe == null)
        {
            return null;
        }

        return CreateStagePagination.CreatePagination(recipe, stage);
    }

    private async Task<HandlerResult?> ViewRecipe(long chatId, int recipeId, CancellationToken cancellationToken)
    {
        var recipe = await recipeRepository.Get(chatId,recipeId, cancellationToken);
        UserSessionsStore.UserSessions[chatId].CurrentViewRecipe = recipe;
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
        
        return new HandlerResult(
            message,
            new InlineKeyboardMarkup(buttons));
    }
    private async Task<HandlerResult?> RemoveRecipe(long chatId, int recipeId,
        CancellationToken cancellationToken)
    {
        await recipeRepository.Delete(chatId,recipeId, cancellationToken);
        UserSessionsStore.UserSessions[chatId].RecipeState = RecipeStates.ShowRecipeSelectionOptions;
        return ShowRecipeSelectionOptions(chatId);
    }

    private HandlerResult WaitingNameForSearch(long chatId)
    {
        UserSessionsStore.UserSessions[chatId].RecipeState = RecipeStates.WaitingForRecipeName;
        return new HandlerResult("Введите полное и частичное наименование рецепта.", null);
    }
    
    private async Task<HandlerResult?> SearchByName(long chatId, string text ,CancellationToken cancellationToken)
    {
        var recipes =  await recipeRepository.SearchByName(chatId, text, cancellationToken);
        
        var buttons = CreateRecipePagination.CreatePagination(
            recipes,
            page: 0,
            5);
        UserSessionsStore.UserSessions[chatId].RecipeState = RecipeStates.ShowRecipces;
        UserSessionsStore.UserSessions[chatId].Recipes = recipes;
        return new HandlerResult("Выбери рецепт", buttons);
    }

    private async Task<HandlerResult?> SearchByCategory(long chatId, string callbackData,CancellationToken cancellationToken)
    {
        var recipes =  await recipeRepository.GetListByTelegramIdAndCategory(chatId,int.Parse(callbackData),cancellationToken);
        
        var buttons = CreateRecipePagination.CreatePagination(
            recipes,
            page: 0,
            5);
        UserSessionsStore.UserSessions[chatId].RecipeState = RecipeStates.ShowRecipces;
        UserSessionsStore.UserSessions[chatId].Recipes = recipes;
        return new HandlerResult("Выбери рецепт", buttons);
    }

    private async Task<HandlerResult?> GetRecipesByCateGory(long chatId, CancellationToken cancellationToken)
    {
        var categories = await categoryRepository.GetAllCategories(cancellationToken);
        var buttons = new List<InlineKeyboardButton>();
        foreach (var category in categories)
        {
            buttons.Add(InlineKeyboardButton.WithCallbackData($"{category.Name}", $"{category.Id}"));
        }
        UserSessionsStore.UserSessions[chatId].RecipeState = RecipeStates.WaitingForCategory;
        return new HandlerResult("✅ Выберите категорию.", new InlineKeyboardMarkup(buttons));
    }

    private HandlerResult? GetPageRecipe(long chatId,string callbackData)
    {
        if (callbackData.StartsWith("recipes:page:"))
        {
            var page = int.Parse(
                callbackData["recipes:page:".Length..]);
            
            return new HandlerResult(
                "📋 Ваши рецепты:",
                CreateRecipePagination
                    .CreatePagination(UserSessionsStore.UserSessions[chatId].Recipes, page, 5));
        }
        return null;
    }
     
    private HandlerResult? CancelRecipe(long chatId)
    {
        UserSessionsStore.UserSessions[chatId].UserState = UserStates.Cansel;
        return null;
    }
    private async Task<HandlerResult?> HandleRecipeSelection(
        long chatId,
        string? callbackData,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(callbackData))
            return null;

        if (callbackData.StartsWith("recipes:page:"))
        {
            return GetPageRecipe(chatId, callbackData);
        }

        if (callbackData.StartsWith("recipe:"))
        {
            var recipeIdText = callbackData["recipe:".Length..];

            if (!int.TryParse(recipeIdText, out var recipeId))
                return null;

            return GetRecipeVaribles(
                chatId,
                recipeId,
                cancellationToken);
        }

        return null;
    }

    private HandlerResult GetRecipeVaribles(long chatId, int recipeId, CancellationToken cancellationToken)
    {
        var buttons = new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "👁️ Просмотреть",
                    $"recipe:view:{recipeId}"),

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
        UserSessionsStore.UserSessions[chatId].RecipeState = RecipeStates.WaitMoveForRecipe;
        return new HandlerResult("Выбери действие", buttons);
    }
    
    private async Task<HandlerResult?> GetAllRecipe (long chatId,CancellationToken cancellationToken)
    {
        var recipes =  await recipeRepository.GetListByTelegramId(chatId,cancellationToken);
        
        var buttons = CreateRecipePagination.CreatePagination(
            recipes,
            page: 0,
            5);
        UserSessionsStore.UserSessions[chatId].RecipeState = RecipeStates.ShowRecipces;
        UserSessionsStore.UserSessions[chatId].Recipes = recipes;
        return new HandlerResult("Выбери рецепт", buttons);
    }
    
    private HandlerResult? ShowRecipeSelectionOptions(long chatId)
    {
        if (UserSessionsStore.UserSessions.TryGetValue(chatId, out var session))
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
            session.RecipeState = RecipeStates.WaitingMove;
            return new HandlerResult("✅ Выберите один из вариантов.", new InlineKeyboardMarkup(buttons));
        }

        return null;
    }
}