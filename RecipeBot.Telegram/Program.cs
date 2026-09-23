using System.Reflection;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RecipeBot.Application;
using RecipeBot.Infrastructure;
using RecipeBot.Telegram.BackGroundServices;
using Serilog;

var envPath = Path.Combine(
    Directory.GetCurrentDirectory(),
    ".env");

Env.Load(envPath);
var builder = Host.CreateApplicationBuilder(args);

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