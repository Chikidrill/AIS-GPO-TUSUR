using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using AisGpo.Api.Auth;
using AisGpo.Api.Common;
using AisGpo.Api.Data;
using AisGpo.Api.Services;
using AisGpo.Api.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// Создаём builder ASP.NET Core приложения.
// Через него регистрируются конфигурация,
// Dependency Injection и инфраструктурные сервисы.
var builder = WebApplication.CreateBuilder(args);

// Загружаем ограничения загрузки файлов.
var workUpload = builder.Configuration
    .GetSection(WorkUploadOptions.SectionName)
    .Get<WorkUploadOptions>()
    ?? throw new InvalidOperationException(
        "WorkUpload configuration is required.");

if (workUpload.MaxFiles <= 0)
{
    throw new InvalidOperationException(
        "WorkUpload:MaxFiles must be greater than zero.");
}

if (workUpload.MaxFileSizeBytes <= 0)
{
    throw new InvalidOperationException(
        "WorkUpload:MaxFileSizeBytes must be greater than zero.");
}

if (workUpload.MaxTotalSizeBytes <= 0)
{
    throw new InvalidOperationException(
        "WorkUpload:MaxTotalSizeBytes must be greater than zero.");
}

if (workUpload.MaxRequestBodySizeBytes <= 0)
{
    throw new InvalidOperationException(
        "WorkUpload:MaxRequestBodySizeBytes must be greater than zero.");
}

if (workUpload.MaxRequestBodySizeBytes <
    workUpload.MaxTotalSizeBytes)
{
    throw new InvalidOperationException(
        "WorkUpload:MaxRequestBodySizeBytes must be greater than or equal to MaxTotalSizeBytes.");
}

if (workUpload.AllowedExtensions.Length == 0)
{
    throw new InvalidOperationException(
        "WorkUpload:AllowedExtensions must contain at least one extension.");
}

// Kestrel должен принять запрос целиком,
// прежде чем бизнес-валидатор сможет проверить файлы.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize =
        workUpload.MaxRequestBodySizeBytes;
});

// Ограничение multipart/form-data
// на уровне ASP.NET Core.
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit =
        workUpload.MaxRequestBodySizeBytes;
});

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

// Строка подключения не должна
// незаметно отсутствовать в production.
var postgresConnectionString =
    builder.Configuration
        .GetConnectionString("Postgres");

if (string.IsNullOrWhiteSpace(
        postgresConnectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Postgres is required.");
}

// Регистрируем Entity Framework Core
// и подключение к PostgreSQL.
builder.Services.AddDbContext<AppDbContext>(
    options =>
        options.UseNpgsql(
            postgresConnectionString));

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

if (string.IsNullOrWhiteSpace(
        jwt.Issuer))
{
    throw new InvalidOperationException(
        "Jwt:Issuer is required.");
}

if (string.IsNullOrWhiteSpace(
        jwt.Audience))
{
    throw new InvalidOperationException(
        "Jwt:Audience is required.");
}

if (string.IsNullOrWhiteSpace(
        jwt.Secret))
{
    throw new InvalidOperationException(
        "Jwt:Secret is required.");
}

// HMAC-ключ должен иметь достаточную длину.
if (Encoding.UTF8.GetByteCount(
        jwt.Secret) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Secret must contain at least 32 bytes.");
}

if (jwt.ExpiresMinutes <= 0)
{
    throw new InvalidOperationException(
        "Jwt:ExpiresMinutes must be greater than zero.");
}

// Известный development-secret
// запрещён за пределами Development.
const string developmentJwtSecret =
    "dev-secret-change-me-at-least-32-bytes";

if (!builder.Environment.IsDevelopment() &&
    string.Equals(
        jwt.Secret,
        developmentJwtSecret,
        StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        "The development JWT secret cannot be used outside Development.");
}

// Тестовые пользователи разрешены
// исключительно в Development.
var devSeedEnabled =
    builder.Configuration.GetValue<bool>(
        "DevSeed:Enabled");

if (!builder.Environment.IsDevelopment() &&
    devSeedEnabled)
{
    throw new InvalidOperationException(
        "DevSeed cannot be enabled outside Development.");
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

// Настройки физического файлового хранилища.
builder.Services.Configure<FileStorageOptions>(
    builder.Configuration.GetSection(
        FileStorageOptions.SectionName));

// Ограничения загрузки файлов работ.
builder.Services.Configure<WorkUploadOptions>(
    builder.Configuration.GetSection(
        WorkUploadOptions.SectionName));

// Бизнес-код работает через IFileStorage,
// а конкретной реализацией пока является
// локальное файловое хранилище.
builder.Services.AddSingleton<
    IFileStorage,
    LocalFileStorage>();

// Регистрируем бизнес-сервисы.
//
// Scoped означает:
// один экземпляр сервиса создаётся
// на один HTTP request.
builder.Services.AddScoped<AuthService>();

builder.Services.AddScoped<
    ParticipationApplicationService>();

builder.Services.AddScoped<
    ProjectLeaveRequestService>();

builder.Services.AddScoped<MeProjectService>();

builder.Services.AddScoped<WorkUploadValidator>();

builder.Services.AddScoped<StudentWorkService>();

builder.Services.AddScoped<WorkQueryService>();

builder.Services.AddScoped<WorkFileService>();

builder.Services.AddScoped<WorkReviewService>();

builder.Services.AddScoped<WorkResubmissionService>();

builder.Services.AddScoped<ProjectMembershipService>();

builder.Services.AddScoped<ProjectLifecycleService>();

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