using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AisGpo.Api.Auth;
using AisGpo.Api.Common;
using AisGpo.Api.Contracts;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AisGpo.Api.Services;

/// <summary>
/// Сервис аутентификации пользователей.
///
/// Проверяет учётные данные пользователя и формирует JWT,
/// который используется для последующих авторизованных запросов.
/// </summary>
public sealed class AuthService(
    AppDbContext db,
    IOptions<JwtOptions> options)
{
    /// <summary>
    /// Компонент ASP.NET Core для проверки хеша пароля.
    /// </summary>
    private readonly PasswordHasher<User> _hasher = new();

    /// <summary>
    /// Настройки формирования JWT,
    /// загруженные из конфигурации приложения.
    /// </summary>
    private readonly JwtOptions _jwt = options.Value;

    /// <summary>
    /// Выполняет аутентификацию пользователя
    /// по электронной почте и паролю.
    /// </summary>
    /// <param name="request">Учётные данные пользователя.</param>
    /// <param name="ct">Токен отмены асинхронной операции.</param>
    /// <returns>
    /// JWT access token, его срок действия,
    /// идентификатор и роль пользователя.
    /// </returns>
    /// <exception cref="ApiException">
    /// Возникает с кодом BAD_CREDENTIALS,
    /// если пользователь не найден или пароль неверен.
    /// </exception>
    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken ct)
    {
        // Email нормализуется, чтобы регистр символов
        // не влиял на поиск пользователя.
        var email = request.Email
            .Trim()
            .ToLowerInvariant();

        var user = await db.Users
            .SingleOrDefaultAsync(
                x => x.Email.ToLower() == email,
                ct);

        // В базе хранится только хеш пароля.
        // Введённый пароль сравнивается с ним через PasswordHasher.
        if (user is null ||
            _hasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password) ==
            PasswordVerificationResult.Failed)
        {
            throw new ApiException(
                StatusCodes.Status401Unauthorized,
                "BAD_CREDENTIALS",
                "Invalid email or password.");
        }

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_jwt.ExpiresMinutes);

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwt.Secret));

        // В JWT помещаются:
        // sub  — email пользователя;
        // uid  — внутренний id пользователя;
        // role — его роль в системе.
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims:
            [
                new Claim(
                    JwtRegisteredClaimNames.Sub,
                    user.Email),

                new Claim(
                    "uid",
                    user.Id.ToString()),

                new Claim(
                    ClaimTypes.Role,
                    user.Role.ToString())
            ],
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256));

        return new LoginResponse(
            new JwtSecurityTokenHandler()
                .WriteToken(token),
            "Bearer",
            (long)(expires - now).TotalSeconds,
            user.Id,
            user.Role);
    }
}