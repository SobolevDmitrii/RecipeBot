using MediatR;
using RecipeBot.Application.Models;
using RecipeBot.Application.Abstract;
using RecipeBot.Application.Helpers;
using RecipeBot.Application.Mappers.Extensions;
using RecipeBot.Domain.Abstract.IRepositories;
using RecipeBot.Domain.Enums;
using RecipeBot.Domain.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace RecipeBot.Application.Handlers;

public record EditRecipeRequest(
    long ChatId,
    string? Text,
    string? ImageFileId,
    string? CallbackData,
    string? Caption,
    int UpdateId) : IRequest<HandlerResult?>;

public class EditRecipeHandler(
    IRecipeRepository recipeRepository,
    ICategoryRepository categoryRepository,
    IUserSessionStore userSessionStore) : IRequestHandler<EditRecipeRequest, HandlerResult?>
{
    public async Task<HandlerResult?> Handle(EditRecipeRequest request, CancellationToken cancellationToken)
    {
        var session = await userSessionStore.TryGetSession(request.ChatId);
        if (session == null)
            return new HandlerResult("Сессия истекла. Отправьте /start и выберите рецепт снова.", null, null);

        session.ChatId = request.ChatId;
        session.CurrentUpdateId = request.UpdateId;
        await userSessionStore.AddProcessing(request.ChatId);
        await userSessionStore.SetUserSession(request.ChatId, session);

        var result = await HandleEdit(request, session, cancellationToken);
        await userSessionStore.SetUserSession(request.ChatId, session);
        return result;
    }

    private async Task<HandlerResult?> HandleEdit(
        EditRecipeRequest request, UserSession userSession, CancellationToken cancellationToken)
    {
        var callback = request.CallbackData;

        HandlerResult? result = null;
        if (!string.IsNullOrWhiteSpace(request.Text) || !string.IsNullOrWhiteSpace(request.Caption)
                                                     || !string.IsNullOrWhiteSpace(request.ImageFileId))
        {
            switch (userSession.RecipeState)
            {
                case RecipeStates.AddingStage:
                    return await AddStage(userSession, request.ChatId,
                        string.IsNullOrWhiteSpace(request.Text) ? request.Caption : request.Text,
                        request.ImageFileId);
                case RecipeStates.WaitingForRecipeName:
                    userSession.RecipeDraft.Name = request.Text;
                    break;
                case RecipeStates.WaitingForIngredients:
                    userSession.RecipeDraft.Ingredients = request.Text;
                    break;
                case RecipeStates.WaitingForImageRecipe:
                    userSession.RecipeDraft.UrlImage = request.ImageFileId;
                    break;
                case RecipeStates.WaitingDescription:
                    userSession.CurrentViewStage.Description = request.Text;
                    return await SelectMoveEditStage(userSession, request.ChatId,
                        $"edit:stage:{userSession.CurrentViewStage.Number}");
                    ;
                case RecipeStates.WaitingImageStage:
                    userSession.CurrentViewStage.UrlImage = request.ImageFileId;
                    return await SelectMoveEditStage(userSession, request.ChatId,
                        $"edit:stage:{userSession.CurrentViewStage.Number}");
                    ;
            }

            userSession.RecipeState = RecipeStates.WaitingMove;
            return Menu("✅ Черновик обновлён. Чтобы применить изменения, нажмите «Сохранить».");
        }

        if (callback != null)
        {
            switch (callback)
            {
                case "edit:cancel":
                    ReturnToRecipes(userSession);
                    return new HandlerResult("Редактирование отменено. Рецепт не изменён.", null,
                        Keyboard(("📋 Мои рецепты", "my_recipe")));
                case "edit:back":
                    userSession.RecipeState = RecipeStates.WaitingMove;
                    return Menu($"Редактирование рецепта {userSession.RecipeDraft.Name}");
                case "edit:name":
                    return await WaitingName(userSession, request.ChatId);
                case "edit:ingredients":
                    return await WaitingIngredients(userSession, request.ChatId);
                case "edit:image":
                    return await WaitingImage(userSession, request.ChatId);
                case "edit:remove_image":
                    return await RemoveImage(userSession, request.ChatId);
                case "edit:category":
                    return await GetCategory(userSession, request.ChatId, cancellationToken);
                case "edit:stages":
                    return await GetStages(userSession, request.ChatId);
                case "edit:save":
                    return await Save(request.ChatId, userSession, cancellationToken);
                case "edit:stage:move:back":
                    return await SelectMoveEditStage(userSession, request.ChatId,
                        $"edit:stage:{userSession.CurrentViewStage.Number}");
            }

            switch (userSession.RecipeState)
            {
                case RecipeStates.GetMenuEdit:
                    var recipeId = int.Parse(
                        callback["recipe:edit:".Length..]);
                    return await GetRecipeAndMenu(userSession, request.ChatId, recipeId, cancellationToken);
                case RecipeStates.WaitingForCategory:
                    userSession.RecipeDraft.CategoryId = int.Parse(callback);
                    userSession.RecipeState = RecipeStates.WaitingMove;
                    return Menu("✅ Черновик обновлён. Чтобы применить изменения, нажмите «Сохранить».");
                case RecipeStates.WaitingForSelectStage:
                    return await SelectMoveEditStage(userSession, request.ChatId, callback);
                case RecipeStates.WaitingMoveForEditStage:
                    return await GetDescriptionEditStage(userSession, request.ChatId, callback);
            }
        }

        return result;
    }

    private async Task<HandlerResult?> SaveStage(UserSession userSession, long chatId)
    {
        var currentStage = userSession.CurrentViewStage;
        userSession.RecipeDraft.Stages.Remove(
            userSession.RecipeDraft.Stages.First(q => q.Number == currentStage.Number));
        userSession.RecipeDraft.Stages.Add(currentStage);
        userSession.RecipeDraft.Stages =
            userSession.RecipeDraft.Stages.OrderBy(q => q.Number).ToList();
        return await GetStages(userSession, chatId);
    }

    private async Task<HandlerResult> WaitingDescription(long chatId, UserSession userSession)
    {
        userSession.RecipeState = RecipeStates.AddingStage;
        await userSessionStore.SetUserSession(chatId, userSession);

        return new HandlerResult($"""
                                  ✏️ В первой строке укажите номер этапа
                                  В следующих строках введите описание этапа, если хотите добавить картинку этапа, просто пришлите ее вместе с текстом:
                                  """, null,
            Keyboard(
                ("↩️ Назад", "edit:stages"),
                ("❌ Отменить редактирование", "edit:cancel")));
    }

    private async Task<HandlerResult?> SelectMoveEditStage(UserSession userSession, long chatId, string callback)
    {
        if (callback == "edit:stages:back")
        {
            var draft = userSession.RecipeDraft;
            userSession.RecipeState = RecipeStates.WaitingMove;
            await userSessionStore.SetUserSession(chatId, userSession);
            return Menu($"✏️ Редактирование рецепта «{draft.Name}». Выберите поле.");
        }

        if (callback == "edit:stages:add")
        {
            return await WaitingDescription(chatId, userSession);
        }

        StageDto stages;
        var stageNumber = int.Parse(
            callback["edit:stage:".Length..]);
        if (userSession.CurrentViewStage == null || stageNumber != userSession.CurrentViewStage.Number)
        {
            stages = userSession.RecipeDraft.Stages.First(s => s.Number == stageNumber);
            userSession.CurrentViewStage = stages;
        }
        else
        {
            stages = userSession.CurrentViewStage;
        }

        userSession.RecipeState = RecipeStates.WaitingMoveForEditStage;
        await userSessionStore.SetUserSession(chatId, userSession);
        return new HandlerResult($"""
                                  {stages.Description}
                                  Выберите действие
                                  """, stages.UrlImage, Keyboard(
            ("Изменить фото", "edit:stage:photo"),
            ("Удалить фото", "edit:stage:delete:photo"),
            ("Изменить описание", "edit:stage:text"),
            ("Удалить", "edit:stage:delete"),
            ("Сохранить", "edit:stage:save"),
            ("Назад", "edit:stage:back")
        ));
    }

    private async Task<HandlerResult?> GetDescriptionEditStage(UserSession userSession, long chatId, string callback)
    {
        switch (callback)
        {
            case "edit:stage:photo":
                userSession.RecipeState = RecipeStates.WaitingImageStage;
                return MessageEditStage("Отправьте новое фото", null);
            case "edit:stage:delete:photo":
                return await DelePhoto(userSession, chatId);
            case "edit:stage:text":
                userSession.RecipeState = RecipeStates.WaitingDescription;
                return MessageEditStage("Пришлите новое описание этапа", null);
            case "edit:stage:delete":
                return await DeleteStage(userSession, chatId);
            case "edit:stage:back":
                return await GetStages(userSession, chatId);
            case "edit:stage:save":
                return await SaveStage(userSession, chatId);
            default:
                return await SelectMoveEditStage(userSession, chatId,
                    $"edit:stage:{userSession.CurrentViewStage.Number}");
        }
    }

    private async Task<HandlerResult?> AddStage(
        UserSession userSession, long chatId, string? text, string? imageFileId)
    {
        userSession.CurrentViewStage = null;
        var stages = userSession.RecipeDraft!.Stages;
        var lines = (text ?? string.Empty).Split('\n', 2);
        if (!int.TryParse(lines[0].Trim(), out var number) || number < 1 || number > stages.Count + 1)
            return new HandlerResult(
                $"В первой строке укажите номер этапа от 1 до {stages.Count + 1}, в следующих строках — описание.",
                null,
                Keyboard(
                    ("↩️ Назад", "edit:stages"),
                    ("❌ Отменить редактирование", "edit:cancel")));

        var description = lines.Length == 2 ? lines[1].Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(description))
            return new HandlerResult("После номера этапа введите описание с новой строки.", null,
                Keyboard(
                    ("↩️ Назад", "edit:stages"),
                    ("❌ Отменить редактирование", "edit:cancel")));

        foreach (var stage in stages.Where(stage => stage.Number >= number))
            stage.Number++;

        stages.Add(new StageDto
        {
            Number = number,
            Description = description,
            UrlImage = imageFileId
        });
        userSession.RecipeDraft.Stages = stages.OrderBy(stage => stage.Number).ToList();
        await userSessionStore.SetUserSession(chatId, userSession);
        return await GetStages(userSession, chatId);
    }

    private async Task<HandlerResult?> DeleteStage(UserSession userSession, long chatId)
    {
        var stageRemove = userSession.CurrentViewStage;
        userSession.RecipeDraft.Stages.Remove(
            userSession.RecipeDraft.Stages.First(q => q.Number == stageRemove.Number));
        userSession.RecipeDraft.Stages =
            userSession.RecipeDraft.Stages.OrderBy(q => q.Number).ToList();
        var number = 1;
        foreach (var stage in userSession.RecipeDraft.Stages)
        {
            stage.Number = number;
            number++;
        }

        userSession.CurrentViewStage = null;
        return await GetStages(userSession, chatId);
    }

    private async Task<HandlerResult?> DelePhoto(UserSession userSession, long chatId)
    {
        userSession.CurrentViewStage.UrlImage = null;
        return await SelectMoveEditStage(userSession, chatId, $"edit:stage:{userSession.CurrentViewStage.Number}");
    }

    private static HandlerResult MessageEditStage(string message, string? urlPhoto) => new(message, urlPhoto, Keyboard(
        ("↩️ Назад", "edit:stage:move:back"),
        ("❌ Отменить редактирование", "edit:cancel")));

    private async Task<HandlerResult?> GetCategory(UserSession userSession, long chatId,
        CancellationToken cancellationToken)
    {
        var categories = (await categoryRepository.GetAllCategories(cancellationToken)).ToDto();
        var buttons = CreateButtonCategory.CreateButtonCategories(categories);

        userSession.RecipeState = RecipeStates.WaitingForCategory;
        await userSessionStore.SetUserSession(chatId, userSession);
        return new HandlerResult("✅ Выберите категорию.", null, buttons);
    }

    private async Task<HandlerResult?> GetStages(UserSession userSession, long chatId)
    {
        var stages = userSession.RecipeDraft.Stages;
        var buttons = stages
            .Select(stage =>
                InlineKeyboardButton.WithCallbackData($"Этап {stage.Number}", $"edit:stage:{stage.Number}"))
            .Chunk(1)
            .ToList();
        buttons.Add([InlineKeyboardButton.WithCallbackData("Добавить новый этап", "edit:stages:add")]);
        buttons.Add([InlineKeyboardButton.WithCallbackData("↩️ Назад", "edit:stages:back")]);

        userSession.RecipeState = RecipeStates.WaitingForSelectStage;
        await userSessionStore.SetUserSession(chatId, userSession);
        return new HandlerResult("✅ Выберите этап.", null, new InlineKeyboardMarkup(buttons));
    }

    private async Task<HandlerResult> WaitingName(UserSession userSession, long chatId)
    {
        userSession.RecipeState = RecipeStates.WaitingForRecipeName;
        await userSessionStore.SetUserSession(chatId, userSession);
        return Message("✏️ Отправьте новое название рецепта.", null);
    }

    private async Task<HandlerResult> WaitingImage(UserSession userSession, long chatId)
    {
        userSession.RecipeState = RecipeStates.WaitingForImageRecipe;

        string? urlImage = null;
        if (!string.IsNullOrWhiteSpace(userSession.RecipeDraft.UrlImage))
        {
            urlImage = userSession.RecipeDraft.UrlImage;
        }

        await userSessionStore.SetUserSession(chatId, userSession);
        return Message("🖼️ Отправьте новую фотографию блюда.", urlImage);
    }

    private async Task<HandlerResult> RemoveImage(UserSession userSession, long chatId)
    {
        userSession.RecipeState = RecipeStates.WaitingMove;
        userSession.RecipeDraft.UrlImage = null;
        await userSessionStore.SetUserSession(chatId, userSession);
        return Menu("🖼️ Фотография удалена.");
    }

    private async Task<HandlerResult> WaitingIngredients(UserSession userSession, long chatId)
    {
        userSession.RecipeState = RecipeStates.WaitingForIngredients;
        var ingredients = userSession.RecipeDraft.Ingredients;
        await userSessionStore.SetUserSession(chatId, userSession);
        return Message($"""
                        Текущие ингредиенты:
                        {ingredients}
                        🥕 Отправьте новый список ингредиентов целиком.
                        """, null);
    }

    private async Task<HandlerResult?> GetRecipeAndMenu(UserSession userSession, long chatId, int recipeId,
        CancellationToken cancellationToken)
    {
        RecipeDto draft;
        try
        {
            draft = (await recipeRepository.Get(chatId, recipeId, cancellationToken)).ToDto();
        }
        catch (InvalidOperationException)
        {
            return new HandlerResult("Рецепт не найден. Выберите его снова.", null,
                Keyboard(("📋 Мои рецепты", "my_recipe")));
        }

        userSession.RecipeDraft = draft;
        userSession.RecipeState = RecipeStates.WaitingMove;
        await userSessionStore.SetUserSession(chatId, userSession);
        return Menu($"✏️ Редактирование рецепта «{draft.Name}». Выберите поле.");
    }

    private async Task<HandlerResult> Save(long chatId, UserSession userSession, CancellationToken cancellationToken)
    {
        var draft = userSession.RecipeDraft!;
        if (draft.Id <= 0 || draft.Id > int.MaxValue || string.IsNullOrWhiteSpace(draft.Name) ||
            string.IsNullOrWhiteSpace(draft.Ingredients) || draft.Stages.Count == 0 ||
            draft.Stages.Any(stage => string.IsNullOrWhiteSpace(stage.Description)))
            return Menu("Заполните название, ингредиенты и хотя бы один этап с описанием.");

        if (!(await categoryRepository.GetAllCategories(cancellationToken)).Any(c => c.Id == draft.CategoryId))
            return Menu("Категория больше недоступна. Выберите другую категорию.");

        try
        {
            await recipeRepository.Update(draft.ToEntity(), chatId, (int)draft.Id, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return Menu("Рецепт больше недоступен. Изменения не сохранены; черновик сохранён в сессии.");
        }

        ReturnToRecipes(userSession);
        await userSessionStore.SetUserSession(chatId, userSession);
        return new HandlerResult("✅ Изменения рецепта сохранены.", null,
            Keyboard(("📋 Мои рецепты", "my_recipe")));
    }

    private static void ReturnToRecipes(UserSession session)
    {
        session.UserState = UserStates.GetMyRecipes;
        session.RecipeState = RecipeStates.ShowRecipeSelectionOptions;
    }

    private static HandlerResult Menu(string message) => new(message, null, Keyboard(
        ("✏️ Название", "edit:name"),
        ("📂 Категория", "edit:category"),
        ("🥕 Ингредиенты", "edit:ingredients"),
        ("🖼️ Фотография", "edit:image"),
        ("🗑️ Удалить фотографию", "edit:remove_image"),
        ("📝 Этапы", "edit:stages"),
        ("💾 Сохранить", "edit:save"),
        ("❌ Отменить редактирование", "edit:cancel")));

    private static HandlerResult Message(string message, string? urlPhoto) => new(message, urlPhoto, Keyboard(
        ("↩️ Назад", "edit:back"),
        ("❌ Отменить редактирование", "edit:cancel")));

    private static InlineKeyboardMarkup Keyboard(params (string Text, string Data)[] buttons)
    {
        return new InlineKeyboardMarkup(buttons.Select(button => new[]
        {
            InlineKeyboardButton.WithCallbackData(button.Text, button.Data)
        }));
    }
}