using System.Security.Claims;

namespace AisGpo.Api.Common;

/// <summary>
/// Вспомогательные методы для работы
/// с данными авторизованного пользователя из JWT.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Возвращает идентификатор текущего пользователя
    /// из claim <c>uid</c> JWT-токена.
    /// </summary>
    /// <param name="principal">
    /// Пользователь, сформированный ASP.NET Core
    /// после успешной JWT-аутентификации.
    /// </param>
    /// <returns>Идентификатор авторизованного пользователя.</returns>
    /// <exception cref="ApiException">
    /// Возникает с HTTP 401 и кодом INVALID_TOKEN,
    /// если claim uid отсутствует или содержит некорректное значение.
    /// </exception>
    public static long GetUserId(
        this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue("uid");

        if (!long.TryParse(raw, out var id))
        {
            throw new ApiException(
                StatusCodes.Status401Unauthorized,
                "INVALID_TOKEN",
                "Authenticated user id is missing.");
        }

        return id;
    }
}