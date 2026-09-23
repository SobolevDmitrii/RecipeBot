FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["RecipeBot.Telegram/RecipeBot.Telegram.csproj", "RecipeBot.Telegram/"]
COPY ["RecipeBot.Application/RecipeBot.Application.csproj", "RecipeBot.Application/"]
COPY ["RecipeBot.Infrastructure/RecipeBot.Infrastructure.csproj", "RecipeBot.Infrastructure/"]
COPY ["RecipeBot.Domain/RecipeBot.Domain.csproj", "RecipeBot.Domain/"]
RUN dotnet restore "RecipeBot.Telegram/RecipeBot.Telegram.csproj"
COPY . .
WORKDIR /src/RecipeBot.Telegram
RUN dotnet publish RecipeBot.Telegram.csproj -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
RUN mkdir -p /app/logs && chown "$APP_UID" /app/logs
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "RecipeBot.Telegram.dll"]
