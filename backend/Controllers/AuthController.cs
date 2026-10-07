using AisGpo.Api.Contracts;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

/// <summary>
/// Контроллер аутентификации пользователей.
///
/// Предоставляет публичный endpoint входа в систему
/// и выдачи JWT access token.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    AuthService service) : ControllerBase
{
    /// <summary>
    /// Выполняет вход пользователя в систему.
    /// </summary>
    /// <param name="request">
    /// Электронная почта и пароль пользователя.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// JWT access token, идентификатор и роль пользователя.
    /// </returns>
    /// <remarks>
    /// Endpoint: POST /api/v1/auth/login.
    ///
    /// Метод доступен без предварительной авторизации.
    /// Проверка учётных данных и создание JWT выполняются
    /// в <see cref="AuthService"/>.
    /// </remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken ct) =>
        Ok(await service.LoginAsync(request, ct));
}