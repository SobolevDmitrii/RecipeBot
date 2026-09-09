namespace RecipeBot.Infrastructure.Redis;

public sealed class RedisOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int Database { get; set; }
}