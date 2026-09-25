# 🍳 Recipe Telegram Bot

> Telegram-бот для создания, хранения и просмотра рецептов с пошаговым описанием приготовления и фотографиями.

Текущая версия: **1.3.0** (25 сентября 2026). Изменения релизов — в [CHANGELOG.md](CHANGELOG.md).

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet\&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-14-239120?logo=csharp\&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-18-4169E1?logo=postgresql\&logoColor=white)](https://www.postgresql.org/)
[![EF Core](https://img.shields.io/badge/EF%20Core-10-512BD4?logo=dotnet\&logoColor=white)](https://learn.microsoft.com/ef/core/)
[![Redis](https://img.shields.io/badge/Redis-8-DC382D?logo=redis\&logoColor=white)](https://redis.io/)
[![Docker](https://img.shields.io/badge/Docker-supported-2496ED?logo=docker\&logoColor=white)](https://www.docker.com/)
[![Telegram Bot API](https://img.shields.io/badge/Telegram-Bot%20API-26A5E4?logo=telegram\&logoColor=white)](https://core.telegram.org/bots/api)
[![Архитектура](https://img.shields.io/badge/архитектура-Clean%20Architecture-success)](#архитектура)

---

## 📌 О проекте

**Recipe Telegram Bot** — pet-проект на современном .NET для создания, хранения и просмотра рецептов непосредственно через Telegram.

Проект демонстрирует построение backend-приложения с разделением ответственности между слоями, пользовательскими state-based сценариями, PostgreSQL для постоянного хранения данных и Redis для хранения пользовательских сессий.

### Основные возможности

* создание рецептов;
* выбор категории;
* добавление ингредиентов;
* добавление фотографии рецепта;
* добавление этапов приготовления;
* добавление фотографии к этапу;
* просмотр собственных рецептов;
* поиск рецептов по названию;
* фильтрация по категории;
* пагинация;
* просмотр этапов приготовления;
* редактирование рецептов и этапов с сохранением черновика;
* удаление рецептов;
* хранение пользовательских сессий в Redis;
* обнаружение зависшей обработки сессий и уведомление пользователя.

Проект создан в учебных и портфолио-целях.

---
# 🎯 Цели проекта

Основная цель проекта — практическое применение современных подходов backend-разработки на .NET.

В проекте используются:

* Clean Architecture;
* domain-oriented подход;
* Dependency Injection;
* MediatR;
* Repository pattern;
* state-based пользовательские сценарии;
* Entity Framework Core;
* PostgreSQL;
* Redis;
* асинхронное программирование;
* BackgroundService;
* интеграция с Telegram Bot API;
* callback-based навигация;
* Docker;
* DTO и сериализация состояния.

Проект предназначен для демонстрации практических навыков .NET backend-разработки.

---
# ✨ Возможности

## 📝 Создание рецепта

Пользователь проходит последовательный сценарий:

```text
/start
   │
   ▼
Главное меню
   │
   ▼
Новый рецепт
   │
   ▼
Название
   │
   ▼
Категория
   │
   ▼
Ингредиенты
   │
   ▼
Фото рецепта
(опционально)
   │
   ▼
Этапы приготовления
   │
   ▼
Сохранение
   │
   ▼
PostgreSQL
```

Каждый этап приготовления содержит:

* обязательное описание;
* фотографию — опционально.

Фотография рецепта и фотография этапа являются разными сценариями state machine.

---

## 📚 Мои рецепты

В разделе **«Мои рецепты»** доступны:

* просмотр собственных рецептов;
* поиск по полному или частичному названию;
* фильтрация по категории;
* постраничная навигация;
* просмотр выбранного рецепта;
* просмотр этапов приготовления;
* удаление рецепта.

---

## 🔎 Поиск

Поддерживается поиск рецептов по полному или частичному названию.

После выполнения поиска пользователь получает найденные рецепты с возможностью постраничной навигации.

---

## ✏️ Редактирование рецепта

Выберите сохранённый рецепт в разделе «Мои рецепты» и откройте редактирование.

| Что можно изменить | Доступные действия |
| :--- | :--- |
| Название и ингредиенты | Заменить название или список ингредиентов |
| Категория | Выбрать другую категорию |
| Фотография блюда | Заменить или удалить |
| Этапы приготовления | Изменить описание, заменить или удалить фотографию, удалить этап |
| Новый этап | Вставить в выбранную позицию с автоматическим сдвигом номеров |

Изменения накапливаются в черновике Redis и записываются в PostgreSQL только после нажатия «Сохранить». «Отменить редактирование» оставляет сохранённый рецепт без изменений. Переход назад в меню редактирования сохраняет черновик.

Для вставки этапа отправьте номер первой строкой, а описание — со следующей строки. Можно приложить фотографию и указать эти данные в подписи. После удаления этапа оставшиеся этапы перенумеровываются.

Перед сохранением проверяются название, ингредиенты, наличие хотя бы одного этапа с описанием и доступность категории. Обновление рецепта ограничено владельцем. Если рецепт уже удалён, изменения не записываются, а черновик остаётся в сессии.

---

## 📂 Фильтрация

Рецепты можно фильтровать по категории.

Пример:

```text
📂 Выберите категорию

[ Супы ]
[ Салаты ]
[ Выпечка ]
[ Десерты ]
```

---

## 📄 Пагинация

Для навигации используются callback-запросы.

Примеры:

```text
recipes:page:0
recipes:page:1
recipe:view:15
recipe:stages:2
```

## 📷 Работа с фотографиями

Фотография может быть добавлена:

* к самому рецепту;
* к этапу приготовления.

При этом фотография этапа **не является самостоятельным этапом**.

Корректный сценарий:

```text
Описание этапа
      +
Фотография (опционально)
      ↓
    Stage
```

Описание этапа является обязательным, фотография — дополнительной информацией.

---

# 🏗 Архитектура

Проект использует принципы **Clean Architecture** и разделён на четыре основных слоя:

```text
┌───────────────────────────────┐
│       RecipeBot.Telegram      │
│   Telegram Bot API / Hosting  │
└───────────────┬───────────────┘
                │
                ▼
┌───────────────────────────────┐
│      RecipeBot.Application    │
│   Handlers / State Machine    │
│      DTO / User Session       │
└───────┬─────────────────┬─────┘
        │                 │
        ▼                 ▼
┌───────────────┐   ┌────────────────────┐
│ RecipeBot.    │   │ RecipeBot.         │
│ Domain        │   │ Infrastructure     │
│               │   │                    │
│ Entities      │   │ EF Core / Redis    │
│ Interfaces    │   │ Repositories       │
└───────────────┘   └─────────┬──────────┘
                              │
                    ┌─────────┴─────────┐
                    ▼                   ▼
               PostgreSQL             Redis
```

### `RecipeBot.Domain`

Содержит предметную область:

* `User`;
* `Recipe`;
* `Stage`;
* `Category`;
* интерфейсы репозиториев;
* состояния пользовательских сценариев.

Доменная модель содержит методы изменения состояния и проверки инвариантов.

Например:

```csharp
recipe.SetName(name);
recipe.SetIngredients(ingredients);
recipe.AddStage(stage);
```

---

### `RecipeBot.Application`

Содержит прикладную логику и пользовательские сценарии.

Основные компоненты:

* `AddNewRecipeHandler`;
* `GetUserRecipesHandler`;
* `EditRecipeHandler`;
* `StartMenuHandler`;
* `GetOrCreateUserHandler`;
* `UserSession`;
* DTO;
* пагинация рецептов;
* пагинация этапов;
* state machine.

Для обработки application requests используется **MediatR**.

---

### `RecipeBot.Infrastructure`

Отвечает за внешние инфраструктурные зависимости:

* Entity Framework Core;
* PostgreSQL;
* Redis;
* `StackExchange.Redis`;
* repositories;
* EF Core migrations;
* Entity Type Configurations.

---

### `RecipeBot.Telegram`

Содержит интеграцию с Telegram Bot API.

Основной компонент:

```text
TelegramBotService
```

Он:

1. получает Telegram updates;
2. определяет тип события;
3. определяет состояние пользовательского сценария;
4. формирует application request;
5. передаёт request через MediatR;
6. получает результат;
7. отправляет ответ пользователю.

В настоящий момент обрабатываются:

```text
Message
CallbackQuery
```

---

# 🧠 Управление состоянием

Взаимодействие с пользователем построено на state machine.

Для каждого `ChatId` хранится `UserSession`.

```text
UserSession
    │
    ├── UserState
    ├── RecipeState
    ├── RecipeDraft
    ├── CurrentViewRecipe
    ├── CurrentViewStage
    ├── ChatId / CurrentUpdateId / LastUpdateId
    └── Recipes
```

Сессия используется для хранения временного состояния пользовательского сценария.

Например:

* состояние пользователя;
* состояние создания рецепта;
* черновик рецепта;
* текущий просматриваемый рецепт;
* данные для текущей пагинации.

### Redis

Сессии хранятся в **Redis**, а не в памяти процесса.

Это позволяет не привязывать состояние пользователя к конкретному экземпляру приложения.

Сессия сериализуется в JSON и сохраняется в Redis через отдельный `IRedisService`.

```text
Telegram
    │
    ▼
UserSessionStore
    │
    ▼
IRedisService
    │
    ▼
Redis
```

Для Redis используется `StackExchange.Redis` и singleton `IConnectionMultiplexer`.

### Обнаружение зависших сессий

При создании, просмотре и редактировании рецептов в сессии сохраняются `ChatId` и `CurrentUpdateId`, а в Redis Sorted Set `processing:sessions` — время начала обработки. После успешной обработки Telegram-сервис очищает маркер обновления и удаляет запись из этого набора.

Фоновый `SessionRecoveryService` каждые пять минут проверяет записи старше пяти минут. Если в сессии остался `CurrentUpdateId`, сервис пытается отправить пользователю уведомление с предложением выполнить `/start` или `/cancel`, затем удаляет запись из списка обрабатываемых. Автоматического повторного выполнения прерванного действия нет.

---

# 🔀 State Machine

Создание рецепта построено как последовательность состояний:

```text
WaitingForRecipeName
        │
        ▼
WaitingForCategory
        │
        ▼
WaitingForIngredients
        │
        ▼
WaitingMove
     ┌──┴──────────────┐
     │                 │
     ▼                 ▼
WaitingForImageRecipe  AddingStage
     │                 │
     ▼                 ▼
WaitingMove        AddingStage
                       │
                       ▼
                   WaitingMove
```

Состояние определяет, как интерпретировать следующее сообщение пользователя.

Например:

```text
WaitingForImageRecipe
```

ожидает фотографию основного изображения рецепта.

А:

```text
AddingStage
```

ожидает описание нового этапа и, при необходимости, фотографию вместе с ним.

Такой подход позволяет не передавать всю информацию о текущем сценарии в каждом Telegram callback.

---

# 🔘 Callback routing

Для inline-клавиатур используются callback-данные.

Примеры:

```text
recipes:page:0
recipe:view:15
recipe:stages:2
recipe:delete:15
recipe:back
recipe:edit:15
edit:name
edit:stages
edit:save
edit:cancel
```

Структура callback позволяет определить:

```text
Entity
Action
Parameters
```

Например:

```text
recipe:stages:2
```

означает:

```text
Entity     = recipe
Action     = stages
StageIndex = 2
```

---

# 🗄 Хранение данных

## PostgreSQL

PostgreSQL используется для постоянного хранения:

```text
User
  │
  └── Recipe
         │
         ├── Category
         │
         └── Stage
```

Для работы с PostgreSQL используется:

* Entity Framework Core 10;
* Npgsql;
* `IDbContextFactory`;
* Repository pattern;
* EF Core migrations;
* Entity Type Configurations.

---

## Redis

Redis используется для временного состояния пользовательских сценариев.

В Redis хранится сериализованный `UserSession`.

Постоянные данные рецептов в Redis не хранятся — они находятся в PostgreSQL.

---

# 📂 Структура проекта

```text
RecipeBot/
│
├── RecipeBot.Domain/
│   ├── Abstract/
│   │   └── IRepositories/
│   ├── Enums/
│   └── Models/
│
├── RecipeBot.Application/
│   ├── Abstract/
│   ├── Handlers/
│   ├── Helpers/
│   │   ├── CreateButtonCategory.cs
│   │   ├── CreateRecipePagination.cs
│   │   └── CreateStagePagination.cs
│   ├── Mappers/
│   ├── Models/
│   └── ApplicationExtension.cs
│
├── RecipeBot.Infrastructure/
│   ├── EntityTypeConfigurations/
│   ├── Migrations/
│   ├── Redis/
│   ├── Repositories/
│   ├── InfraDataExtensions.cs
│   ├── UserSessinsStore.cs
│   └── MyRecipeDbContext.cs
│
├── RecipeBot.Telegram/
│   ├── BackGroundServices/
│   │   ├── TelegramBotService.cs
│   │   └── SessionRecoveryService.cs
│   ├── Program.cs
│   ├── ExceptionHandler.cs
│   ├── Dockerfile
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── appsettings.Testing.json
│   └── appsettings.Production.json
│
├── RecipeBot.UnitTests/
├── RecipeBot.IntegrationTests/
│   └── Repositories/
├── redis/
│   ├── redis.conf
│   └── users.acl
├── compose.yml
├── compose.dev.yml
├── .dockerignore
├── .env.example
├── .gitignore
├── CHANGELOG.md
├── CONTRIBUTING.md
├── LICENSE
└── RecipeBot.sln
```

---

# 🛠 Технологический стек

| Технология                   | Назначение                           |
| ---------------------------- | ------------------------------------ |
| **.NET 10**                  | Платформа приложения                 |
| **C# 14**                    | Язык программирования                |
| **Telegram.Bot**             | Telegram Bot API                     |
| **MediatR**                  | Application request/handler pipeline |
| **Entity Framework Core 10** | ORM                                  |
| **PostgreSQL 18**            | Постоянное хранение данных           |
| **Npgsql**                   | PostgreSQL provider                  |
| **Redis**                    | Хранение пользовательских сессий     |
| **StackExchange.Redis**      | Работа с Redis                       |
| **Docker**                   | Контейнеризация                      |
| **DotNetEnv**                | Загрузка переменных окружения        |
| **Serilog**                  | Файловые логи и контекст окружения   |
| **NUnit / Moq / AutoFixture** | Тестирование и подготовка тестовых данных |
| **Testcontainers**           | PostgreSQL и Redis для интеграционных тестов |
| **coverlet.collector**       | Сбор покрытия тестов                 |

---

# 💉 Dependency Injection

Регистрация зависимостей разделена по слоям.

Основные регистрации в `Program.cs` (сокращённый пример):

```csharp
var builder = Host.CreateApplicationBuilder(args);

var configuration = builder.Configuration;

builder.Services.AddApplication(configuration);
builder.Services.AddInfra(configuration);

builder.Services.AddHostedService<TelegramBotService>();
builder.Services.AddHostedService<SessionRecoveryService>();
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

var app = builder.Build();

app.Run();
```

Infrastructure регистрирует:

* `DbContextFactory`;
* repositories;
* Redis;
* `IConnectionMultiplexer`;
* `IRedisService`.

Telegram layer регистрирует:

```text
TelegramBotService
SessionRecoveryService
```

как фоновые `HostedService`.

## Логирование и обработка ошибок

Ошибки обработки обновлений, polling и проверки зависших сессий передаются через MediatR в `ExceptionHandler`. Он записывает исключения Telegram API, EF Core/PostgreSQL, Redis и прочие ошибки через `ILogger`.

Serilog читает настройки из `appsettings.json` и файла выбранного окружения, добавляя свойство `Environment`. Базовая конфигурация включает ежедневную ротацию файлов, ограничение размера 10 МБ и хранение до 30 файлов.

| `DOTNET_ENVIRONMENT` | Файл настроек | Путь логов | Минимальный уровень файлового лога |
| :--- | :--- | :--- | :--- |
| `Development` | `appsettings.Development.json` | `logs/dev-.log` | Debug |
| `Testing` | `appsettings.Testing.json` | `logs/test-.log` | Information |
| `Production` | `appsettings.Production.json` | `logs/prod-.log` | Error |

В Compose используется `Production`, логи сохраняются в volume `bot_logs`.

---

# ⚙️ Конфигурация

Для локальной разработки используется `.env`.

В репозитории хранится только `.env.example`.

Основные параметры:

```env
TelegramApiToken=
DOTNET_ENVIRONMENT=Development

ConnectionStrings__MyRecipeDb=

Redis__ConnectionString=
Redis__User=
Redis__Password=
Redis__Database=
```

Для полного запуска Compose дополнительно используются `POSTGRES_USER`, `POSTGRES_DB`, `POSTGRES_PASSWORD`, `REDIS_PASSWORD` и необязательный `RECIPEBOT_IMAGE`. Локальные `Redis__Password` и пароль в строке подключения должны соответствовать паролям инфраструктуры.

`.env` загружается из текущего каталога, если файл существует. `Env.NoClobber()` сохраняет приоритет уже заданных переменных окружения. Параметр `DOTNET_ENVIRONMENT` выбирает файл настроек окружения.

> Реальный `.env` не должен попадать в Git-репозиторий.

Для Docker и production рекомендуется использовать environment variables или Docker Secrets / внешний менеджер секретов.

---

# 🗄 Миграции

Применить миграции встроенным режимом приложения без запуска бота:

```bash
dotnet run --project RecipeBot.Telegram -- --migrate
```

Этот режим использует подключение PostgreSQL и завершает процесс после применения миграций. В Compose его запускает отдельный сервис `migrate`.

Применить существующие миграции:

```bash
dotnet ef database update \
  --project RecipeBot.Infrastructure \
  --startup-project RecipeBot.Telegram
```

Создать новую миграцию:

```bash
dotnet ef migrations add MigrationName \
  --project RecipeBot.Infrastructure \
  --startup-project RecipeBot.Telegram
```

---

# 🚀 Запуск проекта

## Требования

Перед запуском необходимо установить:

* .NET 10 SDK;
* PostgreSQL 18;
* Redis;
* Telegram-бота, созданного через BotFather.

Docker можно использовать для запуска инфраструктуры.

---

## Локальный запуск

В корне репозитория создайте `.env` на основе `.env.example` и заполните токен бота и параметры подключения к PostgreSQL и Redis. Приложение загружает `.env` из текущего рабочего каталога, поэтому команды ниже выполняются из корня репозитория.

Запустите PostgreSQL и Redis с compose.dev.yml (см. раздел Docker) до применения миграций.

Восстановить зависимости:

```bash
dotnet restore
```

Собрать решение:

```bash
dotnet build
```

Применить миграции:

```bash
dotnet ef database update \
  --project RecipeBot.Infrastructure \
  --startup-project RecipeBot.Telegram
```

Запустить:

```bash
dotnet run --project RecipeBot.Telegram
```

После запуска приложение начинает получать обновления от Telegram.

---

# 🐳 Docker

Compose запускает PostgreSQL 18, Redis 8, отдельный контейнер миграций и бота.
Бот стартует после успешных миграций и готовности Redis. Для long polling запускайте
только один экземпляр бота с одним Telegram-токеном.

## Настройка и запуск

Скопируйте `.env.example` в `.env` (существующий `.env` не перезаписывайте).
Заполните `TelegramApiToken`, `POSTGRES_PASSWORD`, `REDIS_PASSWORD`.
Для паролей удобно использовать случайные строки из 64 шестнадцатеричных символов.
`POSTGRES_USER` и `POSTGRES_DB` имеют значения по умолчанию.
Compose сам задаёт адреса `postgres` и `redis`, независимо от локальных строк подключения.

```bash
docker compose up -d --build
docker compose ps -a
docker compose logs --tail=100 migrate bot
```

Контейнер `migrate` должен завершиться с кодом 0. Файловые логи Serilog сохраняются
в volume `bot_logs`, каталог `/app/logs` доступен непривилегированному пользователю.
Данные PostgreSQL и Redis сохраняются в `postgres_data` и `redis_data`.
Порты баз данных наружу не публикуются. Старые файлы `redis/redis.conf` и
`redis/users.acl` больше не используются: пароль Redis берётся из `.env`, пользователь — `default`.

При подключении существующего PostgreSQL volume используйте его прежние имя БД,
пользователя и пароль: изменение `.env` не меняет пароль уже созданной базы.
Перед обновлениями сохраняйте резервную копию БД и проверяйте новые миграции.
`docker compose down` сохраняет данные; добавление `-v` удаляет volumes вместе с данными.

## Локальная разработка

Для запуска бота через `dotnet run` поднимите только инфраструктуру и опубликуйте
её порты на localhost:

```bash
docker compose -f compose.yml -f compose.dev.yml up -d postgres redis
```

Заполните локальные `ConnectionStrings__MyRecipeDb`, `Redis__Password` в `.env`
теми же паролями. Используйте `Redis__User=default`, адреса `localhost:5432` и
`localhost:6379`. Затем примените миграции и запустите бота по инструкции выше.
Остановите контейнер `bot`, если ранее запускали полный Compose, чтобы избежать
одновременного polling одним токеном.

## Публикация образа в Docker Hub

Замените `YOUR_ACCOUNT` своим Docker Hub namespace, а `1.3.0` — версией релиза:

```bash
docker login
docker build -f RecipeBot.Telegram/Dockerfile -t YOUR_ACCOUNT/recipebot:1.3.0 .
docker push YOUR_ACCOUNT/recipebot:1.3.0
```

На сервер перенесите `compose.yml`, создайте `.env` с секретами и задайте
`RECIPEBOT_IMAGE=YOUR_ACCOUNT/recipebot:1.3.0`. Исходники и SDK на сервере не нужны:

```bash
docker compose pull
docker compose up -d --no-build
```

Для обновления измените тег в `.env` и повторите эти команды. Контейнер миграций
использует тот же образ, что и бот. Сборка по умолчанию предназначена для архитектуры
машины сборки; для сервера другой архитектуры используйте Docker Buildx.
Секреты `.env` исключены из Git и контекста сборки; не добавляйте их в образ.

---

# 🧪 Тестирование

В решении два тестовых проекта:

* `RecipeBot.UnitTests` — модульные тесты обработчиков создания, просмотра и редактирования рецептов, стартового меню, `TelegramBotService` и `SessionRecoveryService`. Используются NUnit, Moq и AutoFixture; внешние сервисы не нужны.
* `RecipeBot.IntegrationTests` — интеграционные тесты, проверка репозиториев и пользовательских сценариев с настоящими PostgreSQL и Redis через Testcontainers. Тесты автоматически запускают отдельные контейнеры, применяют миграции PostgreSQL и освобождают контейнеры после выполнения.

Редактирование проверяется от изменения черновика до сохранения в БД: изменение полей и этапов, вставка и удаление этапов, удаление фотографий, отмена, недоступная категория или рецепт и запрет изменения чужого рецепта.

Запуск модульных тестов:

```bash
dotnet test RecipeBot.UnitTests
```

Для интеграционных тестов нужен запущенный Docker с поддержкой Linux-контейнеров и возможность загрузить образы `postgres:16-alpine` и `redis:7-alpine`. Эти версии заданы в тестах и отличаются от версий в `compose.yml`. Запуск Compose и настройка рабочего `.env` для тестов не требуются.

```bash
dotnet test RecipeBot.IntegrationTests
```

Запуск всех тестов со сбором покрытия:

```bash
dotnet test RecipeBot.sln --collect:"XPlat Code Coverage" --results-directory TestResults
```

Файлы `coverage.cobertura.xml` сохраняются во вложенных каталогах `TestResults`.

## Покрытие тестами

Результаты замера для версии **1.3.0** по предоставленному отчёту от 25 сентября 2026:

| Проект | Покрытие |
| :--- | ---: |
| `RecipeBot.Application` | 93% |
| `RecipeBot.Infrastructure` | 82% |
| `RecipeBot.Domain` | 74% |
| `RecipeBot.Telegram` | 64% |
| **Всего по проекту** | **84%** |

Проценты приведены с округлением из отчёта. Таблица отражает сохранённый результат замера и не обновляется автоматически при изменении кода.

---

# 🔐 Безопасность

Не храните в Git:

* Telegram Bot Token;
* пароли PostgreSQL;
* пароли Redis;
* другие секреты.

Для локальной разработки используются переменные окружения.

Для production рекомендуется использовать:

* environment variables;
* Docker Secrets;
* Vault или другой внешний secret manager.

---

# 📈 Возможные направления развития

Текущая версия уже поддерживает:

* создание рецептов;
* фотографии;
* категории;
* поиск;
* фильтрацию;
* пагинацию;
* просмотр рецептов;
* просмотр этапов;
* удаление;
* Redis-сессии;
* unit-тесты;
* integration-тесты;
* редактирование рецептов и этапов;
* централизованное логирование ошибок;
* файловые логи Serilog;
* запуск бота и миграций через Compose с проверками готовности PostgreSQL и Redis.

Возможные дальнейшие улучшения:

* OpenTelemetry;
* health checks приложения;
* CI/CD;
* production deployment;

---

## 📄 Лицензия

Проект распространяется под лицензией **MIT**.

Подробнее: [LICENSE](LICENSE).
