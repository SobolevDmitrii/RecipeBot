using Microsoft.Extensions.Hosting;
using RecipeBot.Application;
using RecipeBot.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// Add services to the container.
var configuration = builder.Configuration;
builder.Services.AddApplication(configuration);
builder.Services.AddInfra(configuration);

var app = builder.Build();


app.Run();