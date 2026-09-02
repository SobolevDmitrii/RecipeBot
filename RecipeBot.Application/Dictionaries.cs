//using MyRecipeApplication.Handlers;

using MyRecipeDomain.Models;
using RecipeBot.Domain.Enums;

namespace RecipeBot.Application;

public static class Dictionaries
{
    public static Dictionary<long, States> UserState = new();
    public static Dictionary<long, States> RecipeState = new();
    public static Dictionary<long, Recipe> RecipeDrafts  = new();
}