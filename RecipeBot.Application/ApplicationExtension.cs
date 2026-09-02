using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecipeBot.Application.BackGroundServices;
using Telegram.Bot;

namespace RecipeBot.Application;

public static class ApplicationExtension
{
    public static void AddApplication(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        serviceCollection.AddHostedService<TelegramBotService>();
        serviceCollection.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        serviceCollection.AddTelegramBot(configuration);
        //serviceCollection.AddSingleton<HandlersFactory>();

    }

    private static void AddTelegramBot(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        var token = configuration.GetSection("TelegramApiToken").Value;

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException(
                $"Требуется указать переменную окружения TelegramApiToken или она пустая");
        }
        var botClient = new TelegramBotClient(token);
        serviceCollection.AddSingleton<ITelegramBotClient>(sp => botClient);
    }
}