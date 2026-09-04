using MediatR;
using RecipeBot.Application.Models;

namespace RecipeBot.Application.Handlers;


public record GetUserRecipesRequest(
long ChatId,
string? Text,
string? CallbackData) : IRequest<HandlerResult?>;

public class GetUserRecipesHandler() : IRequestHandler<GetUserRecipesRequest, HandlerResult?>
{
    public Task<HandlerResult?> Handle(GetUserRecipesRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}