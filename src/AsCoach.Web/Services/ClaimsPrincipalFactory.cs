using System.Security.Claims;
using AsCoach.Web.Domain;

namespace AsCoach.Web.Services;

public static class ClaimsPrincipalFactory
{
    public const string NomeClaim = "nome";

    public static ClaimsPrincipal Crea(Utente utente)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, utente.Id.ToString()),
            new(ClaimTypes.Name, utente.Username),
            new(ClaimTypes.Email, utente.Email),
            new(ClaimTypes.Role, (utente.Ruolo ?? UserRole.USER).ToString()),
            new(NomeClaim, utente.Nome)
        };

        var identity = new ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    public static AppUser? ToAppUser(ClaimsPrincipal principal)
    {
        var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(idValue, out var id))
        {
            return null;
        }

        var ruolo = Enum.TryParse<UserRole>(principal.FindFirstValue(ClaimTypes.Role), out var parsed)
            ? parsed
            : UserRole.USER;

        return new AppUser(
            id,
            principal.FindFirstValue(NomeClaim) ?? principal.Identity?.Name ?? string.Empty,
            principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            principal.Identity?.Name ?? string.Empty,
            ruolo);
    }
}
