namespace AisGpo.Api.Auth;

/// <summary>
/// Настройки JWT-аутентификации.
///
/// Значения загружаются из секции <c>Jwt</c>
/// конфигурационного файла приложения.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>
    /// Имя секции конфигурации, содержащей параметры JWT.
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Издатель JWT-токена.
    /// Используется при проверке поля issuer.
    /// </summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>
    /// Получатель JWT-токена.
    /// Используется при проверке поля audience.
    /// </summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// Секретный ключ, используемый для подписи JWT.
    /// </summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>
    /// Время жизни access token в минутах.
    /// По умолчанию токен действует 120 минут.
    /// </summary>
    public int ExpiresMinutes { get; init; } = 120;
}