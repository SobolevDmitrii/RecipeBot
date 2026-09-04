using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RecipeBot.Application;
using RecipeBot.Infrastructure;
using RecipeBot.Telegram.BackGroundServices;

var builder = Host.CreateApplicationBuilder(args);

// Add services to the container.
var configuration = builder.Configuration;
builder.Services.AddApplication(configuration);
builder.Services.AddInfra(configuration);
builder.Services.AddHostedService<TelegramBotService>();

var app = builder.Build();


app.Run();