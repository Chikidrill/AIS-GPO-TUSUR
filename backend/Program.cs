using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using AisGpo.Api.Auth;
using AisGpo.Api.Common;
using AisGpo.Api.Data;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// Создаём builder ASP.NET Core приложения.
// Через него регистрируются конфигурация,
// Dependency Injection и инфраструктурные сервисы.
var builder = WebApplication.CreateBuilder(args);

// Подключаем MVC-контроллеры.
//
// JsonStringEnumConverter заставляет API
// сериализовать enum как строки:
// OPEN вместо 1,
// STUDENT вместо 2 и т. п.
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter()));

// OpenAPI используется для описания HTTP API
// в development-окружении.
builder.Services.AddOpenApi();

// Глобальная обработка контролируемых
// ApiException и неожиданных исключений.
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

// Регистрируем Entity Framework Core
// и подключение к PostgreSQL.
//
// Строка подключения берётся
// из ConnectionStrings:Postgres.
builder.Services.AddDbContext<AppDbContext>(
    options =>
        options.UseNpgsql(
            builder.Configuration
                .GetConnectionString("Postgres")));

// Загружаем JWT-настройки
// из секции Jwt конфигурации.
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(
        JwtOptions.SectionName));

var jwt = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>()
    ?? throw new InvalidOperationException(
        "JWT configuration is required.");

// HMAC-ключ должен иметь достаточную длину.
// При некорректной конфигурации приложение
// не запускается.
if (Encoding.UTF8.GetByteCount(jwt.Secret) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Secret must contain at least 32 bytes.");
}

// Настраиваем JWT Bearer Authentication.
//
// При каждом защищённом HTTP-запросе ASP.NET Core
// проверяет подпись, issuer, audience
// и срок действия access token.
builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,

                ValidateAudience = true,
                ValidAudience = jwt.Audience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwt.Secret)),

                ValidateLifetime = true,

                // Допускается небольшое расхождение
                // системного времени клиента и сервера.
                ClockSkew =
                    TimeSpan.FromSeconds(30),

                // Claim Role используется
                // атрибутами [Authorize(Roles = ...)].
                RoleClaimType =
                    ClaimTypes.Role
            };
    });

// Включаем механизм авторизации ASP.NET Core.
builder.Services.AddAuthorization();

// Регистрируем бизнес-сервисы.
//
// Scoped означает:
// один экземпляр сервиса создаётся
// на один HTTP request.
builder.Services.AddScoped<AuthService>();

builder.Services.AddScoped<
    ParticipationApplicationService>();

builder.Services.AddScoped<MeProjectService>();

// Завершаем построение приложения.
var app = builder.Build();

// Все необработанные исключения проходят
// через ApiExceptionHandler.
app.UseExceptionHandler();

// OpenAPI endpoint публикуется
// только в Development.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Authentication определяет:
// "Кто отправил запрос?"
app.UseAuthentication();

// Authorization определяет:
// "Имеет ли этот пользователь право
// выполнить эту операцию?"
app.UseAuthorization();

// Подключаем endpoint'ы,
// объявленные в Controllers.
app.MapControllers();

// Создаём отдельный DI scope
// для инициализации базы.
//
// DbInitializer применяет migrations
// и при необходимости добавляет DevSeed.
await using (var scope =
    app.Services.CreateAsyncScope())
{
    await DbInitializer.InitializeAsync(
        scope.ServiceProvider,
        builder.Configuration);
}

// Запускаем HTTP-сервер.
app.Run();