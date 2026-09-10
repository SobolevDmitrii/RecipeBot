using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecipeBot.Application.Abstract;
using RecipeBot.Domain.Abstract.IRepositories;
using RecipeBot.Infrastructure.Redis;
using RecipeBot.Infrastructure.Repositories;
using StackExchange.Redis;

//using MyRecipeDomain.Abstract.Repositories;
//using MyRecipeInfraData.Repositories;

namespace RecipeBot.Infrastructure;

public static class InfraDataExtensions
{
    public static void AddInfra(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        serviceCollection.AddMyContext(configuration);
        serviceCollection.AddRedis(configuration);
        serviceCollection.AddScoped<IUsersRepository, UsersRepository>();
        serviceCollection.AddScoped<IRecipeRepository, RecipeRepository>();
        serviceCollection.AddScoped<IStageRepository, StageRepository>();
        serviceCollection.AddScoped<ICategoryRepository, CategoryRepository>();
    }

    private static void AddMyContext(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MyRecipeDb");
        
        serviceCollection.AddDbContextFactory<MyRecipeDbContext>(q => q.UseNpgsql(connectionString));
        serviceCollection.AddSingleton<IUserSessionStore, UserSessionsStore>();
    }

    private static void AddRedis(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        var redisOptions = configuration
                               .GetSection("Redis")
                               .Get<RedisOptions>()
                           ?? throw new InvalidOperationException(
                               "Redis configuration is missing.");

        serviceCollection.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisOptions.ConnectionString);
            options.User = redisOptions.User;
            options.Password = redisOptions.Password;
            options.DefaultDatabase = redisOptions.Database;
            return ConnectionMultiplexer.Connect(options);
        });
        
        serviceCollection.AddSingleton<IRedisService, RedisService>();
    }
}