using MediatR;
using RecipeBot.Domain.Abstract.IRepositories;
using Telegram.Bot.Types;
using User = RecipeBot.Domain.Models.User;

namespace RecipeBot.Application.Handlers;

public record GetOrCreateUserRequest(long ChatId ,string FirstName) : IRequest<User>;


public class GetOrCreateUserHandler (IUsersRepository  usersRepository) : IRequestHandler<GetOrCreateUserRequest,User>
{
    public async Task<User> Handle(GetOrCreateUserRequest request, CancellationToken cancellationToken)
    =>
       await usersRepository
            .GetOrCreateUserAsync(request.ChatId, request.FirstName, cancellationToken);
    
}