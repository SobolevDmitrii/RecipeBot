using MediatR;
using Telegram.Bot.Types;

namespace RecipeBot.Application;

public abstract class MessageBaseRequest(Update update) : IRequest<Update>
{
    public Update Update { get; set; } = update;
}