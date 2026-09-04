using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecipeBot.Domain.Abstract.IRepositories;
using RecipeBot.Infrastructure.Repositories;

//using MyRecipeDomain.Abstract.Repositories;
//using MyRecipeInfraData.Repositories;

namespace RecipeBot.Infrastructure;

public static class InfraDataExtensions
{
    public static void AddInfra(this IServiceCollection serviceCollection,IConfiguration configuration)
    {
        serviceCollection.AddMyContext(configuration);
        serviceCollection.AddScoped<IUsersRepository, UsersRepository>();
        serviceCollection.AddScoped<IRecipeRepository, RecipeRepository>();
        serviceCollection.AddScoped<IStageRepository, StageRepository>();
        serviceCollection.AddScoped<ICategoryRepository, CategoryRepository>();
    }

    private static void AddMyContext(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MyRecipeDb");
        serviceCollection.AddDbContextFactory<MyRecipeDbContext>(q => q.UseNpgsql(connectionString));
    }
}