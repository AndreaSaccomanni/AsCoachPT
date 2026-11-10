using System.Security.Claims;
using AsCoachPT.Application.Models;
using AsCoachPT.Domain.Models;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AsCoachPT.UI.Services;

/// <summary>Costruisce e interpreta l'identità dell'utente autenticato.</summary>
public static class ClaimsPrincipalFactory
{
    /// <summary>Nome del claim che contiene il nome visualizzato.</summary>
    public const string NomeClaim = "nome";

    /// <summary>Versione della sicurezza: cambia quando la password viene reimpostata.</summary>
    public const string VersioneClaim = "versione";

    /// <summary>Crea il principal con identificativo, email, nome e ruolo dell'utente.</summary>
    public static ClaimsPrincipal Crea(Utente utente)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, utente.Id.ToString()),
            new(ClaimTypes.Name, utente.Email),
            new(ClaimTypes.Email, utente.Email),
            new(ClaimTypes.Role, (utente.Ruolo ?? UserRole.USER).ToString()),
            new(NomeClaim, utente.Nome),
            new(VersioneClaim, utente.VersioneSicurezza.ToString())
        };

        var identita = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identita);
    }

    /// <summary>Legge i dati applicativi dal principal, restituendo null se non è autenticato.</summary>
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
