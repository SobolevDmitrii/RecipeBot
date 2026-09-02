FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["RecipeBot.Telegram/RecipeBot.Telegram.csproj", "RecipeBot.Telegram/"]
RUN dotnet restore "RecipeBot.Telegram/RecipeBot.Telegram.csproj"
COPY . .
WORKDIR "/src/RecipeBot.Telegram"
RUN dotnet build "./RecipeBot.Telegram.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./RecipeBot.Telegram.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RecipeBot.Telegram.dll"]
