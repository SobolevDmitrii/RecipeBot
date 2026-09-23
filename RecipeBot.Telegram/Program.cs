using System.Reflection;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RecipeBot.Application;
using RecipeBot.Infrastructure;
using RecipeBot.Telegram.BackGroundServices;
using Serilog;

var envPath = Path.Combine(
    Directory.GetCurrentDirectory(),
    ".env");

if (File.Exists(envPath))
    Env.NoClobber().Load(envPath);
var builder = Host.CreateApplicationBuilder(args);

if (args.Contains("--migrate"))
{
    var options = new DbContextOptionsBuilder<MyRecipeDbContext>()
        .UseNpgsql(builder.Configuration.GetConnectionString("MyRecipeDb"))
        .Options;
    await using var context = new MyRecipeDbContext(options);
    await context.Database.MigrateAsync();
    Console.WriteLine("Database migrations completed.");
    return;
}

// Add services to the container.
var configuration = builder.Configuration;
builder.Services.AddApplication(configuration);
builder.Services.AddInfra(configuration);
builder.Services.AddHostedService<TelegramBotService>();
builder.Services.AddHostedService<SessionRecoveryService>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

builder.Services.AddSerilog((services, config) => config
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName));

var app = builder.Build();


app.Run();
