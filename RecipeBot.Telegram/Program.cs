using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RecipeBot.Application;
using RecipeBot.Infrastructure;
using RecipeBot.Telegram.BackGroundServices;

Env.Load();
var builder = Host.CreateApplicationBuilder(args);

// Add services to the container.
var configuration = builder.Configuration;
builder.Services.AddApplication(configuration);
builder.Services.AddInfra(configuration);
builder.Services.AddHostedService<TelegramBotService>();
builder.Services.AddHostedService<SessionRecoveryService>();

var app = builder.Build();


app.Run();