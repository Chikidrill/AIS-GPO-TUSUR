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

public sealed class AuthService(AppDbContext db, IOptions<JwtOptions> options)
{
    private readonly PasswordHasher<User> _hasher = new();
    private readonly JwtOptions _jwt = options.Value;

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email.ToLower() == email, ct);

        if (user is null || _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            throw new ApiException(StatusCodes.Status401Unauthorized, "BAD_CREDENTIALS", "Invalid email or password.");

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_jwt.ExpiresMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim("uid", user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            ],
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            (long)(expires - now).TotalSeconds,
            user.Id,
            user.Role);
    }
}

