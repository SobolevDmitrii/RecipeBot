using System.Collections.Concurrent;
using RecipeBot.Application.Models;

namespace RecipeBot.Application;

public static class Dictionaries
{
    /*public static ConcurrentDictionary<long, States> UserState = new();
    public static ConcurrentDictionary<long, States> RecipeState = new();
    public static ConcurrentDictionary<long, Recipe> RecipeDrafts  = new();*/
    
    public static readonly ConcurrentDictionary<long, UserSession> UserSessions = new();
}