using System.Security.Claims;

namespace AisGpo.Api.Common;

public static class ClaimsPrincipalExtensions
{
    public static long GetUserId(this ClaimsPrincipal principal)
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

