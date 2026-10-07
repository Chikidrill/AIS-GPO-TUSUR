namespace AisGpo.Api.Common;

/// <summary>
/// Исключение бизнес-уровня, предназначенное
/// для возврата контролируемой ошибки через HTTP API.
///
/// Содержит HTTP-статус и машинный код ошибки,
/// который frontend может использовать для обработки ответа.
/// </summary>
public sealed class ApiException : Exception
{
    /// <summary>
    /// HTTP status code, который должен быть возвращён клиенту.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Машинный код ошибки.
    /// Например: PROJECT_FULL, INVALID_TOKEN или PROJECT_NOT_FOUND.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Создаёт новое исключение API.
    /// </summary>
    /// <param name="statusCode">HTTP status code ответа.</param>
    /// <param name="code">Машинный код ошибки.</param>
    /// <param name="message">Человекочитаемое описание ошибки.</param>
    public ApiException(
        int statusCode,
        string code,
        string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }
}