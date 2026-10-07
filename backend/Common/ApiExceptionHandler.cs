using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Common;

/// <summary>
/// Глобальный обработчик исключений приложения.
///
/// Преобразует ожидаемые <see cref="ApiException"/>
/// в структурированный JSON-ответ API,
/// а неожиданные исключения — в HTTP 500.
/// </summary>
public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>
    /// Обрабатывает исключение, возникшее во время HTTP-запроса.
    /// </summary>
    /// <param name="httpContext">Контекст текущего HTTP-запроса.</param>
    /// <param name="exception">Перехваченное исключение.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>
    /// <see langword="true"/>, если исключение было обработано.
    /// </returns>
    /// <remarks>
    /// Для <see cref="ApiException"/> клиент получает JSON вида:
    /// <c>{ code, message, timestamp }</c>.
    ///
    /// Необработанные исключения журналируются и возвращаются
    /// клиенту как HTTP 500 без раскрытия внутренних деталей.
    /// </remarks>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is ApiException apiException)
        {
            httpContext.Response.StatusCode =
                apiException.StatusCode;

            await httpContext.Response.WriteAsJsonAsync(
                new
                {
                    code = apiException.Code,
                    message = apiException.Message,
                    timestamp = DateTimeOffset.UtcNow
                },
                cancellationToken);

            return true;
        }

        // Неожиданные исключения записываются в лог,
        // но их внутренние детали не передаются клиенту.
        logger.LogError(
            exception,
            "Unhandled exception");

        httpContext.Response.StatusCode =
            StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status =
                    StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "Unexpected server error."
            },
            cancellationToken);

        return true;
    }
}