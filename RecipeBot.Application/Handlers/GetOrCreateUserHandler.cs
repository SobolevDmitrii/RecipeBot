using MediatR;
using RecipeBot.Domain.Abstract.IRepositories;
using Telegram.Bot.Types;
using User = MyRecipeDomain.Models.User;

namespace RecipeBot.Application.Handlers;

public class GetOrCreateUserRequest(Update update): IRequest<User>
{
    public Update Update { get; set; } = update;
}

public class GetOrCreateUserHandler (IUsersRepository  usersRepository) : IRequestHandler<GetOrCreateUserRequest,User>
{
    public async Task<User> Handle(GetOrCreateUserRequest request, CancellationToken cancellationToken)
    =>
       await usersRepository
            .GetOrCreateUserAsync(request.Update.Message.Chat.Id, request.Update.Message.From.FirstName, cancellationToken);
    
}