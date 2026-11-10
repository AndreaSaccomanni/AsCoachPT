using System.Security.Cryptography;
using System.Text;
using AsCoachPT.Application.Repositories;
using AsCoachPT.Domain.Models;

namespace AsCoachPT.Application.Services;

/// <summary>Esito della generazione di un link di reimpostazione password.</summary>
/// <param name="UtenteTrovato">Falso se l'email o l'id non corrispondono a un utente.</param>
/// <param name="Link">Link da inviare o consegnare all'utente, valido 30 minuti.</param>
/// <param name="EmailInviata">Indica se l'email è partita; se falso il link va consegnato a mano.</param>
public sealed record EsitoReimpostazione(bool UtenteTrovato, string? Link, bool EmailInviata)
{
    public static readonly EsitoReimpostazione NonTrovato = new(false, null, false);
}

/// <summary>
/// Gestisce il cambio password e la reimpostazione tramite token monouso.
/// Del token viene salvato solo l'hash; il link scade dopo 30 minuti.
/// </summary>
public sealed class PasswordResetService(
    IUtenteRepository utenti,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender)
{
    public const int LunghezzaMinimaPassword = 8;
    private static readonly TimeSpan ValiditaLink = TimeSpan.FromMinutes(30);

    /// <summary>Genera il link per l'email indicata e prova a inviarlo.</summary>
    public async Task<EsitoReimpostazione> GeneraEInviaAsync(
        string email,
        string baseUrl,
        CancellationToken tokenAnnullamento = default)
    {
        var utente = await utenti.TrovaPerEmailAsync(email.Trim(), tokenAnnullamento);
        return utente is null
            ? EsitoReimpostazione.NonTrovato
            : await GeneraEInviaAsync(utente, baseUrl, tokenAnnullamento);
    }

    /// <summary>Genera il link per un utente scelto dallo staff e prova a inviarlo.</summary>
    public async Task<EsitoReimpostazione> GeneraEInviaPerIdAsync(
        long id,
        string baseUrl,
        CancellationToken tokenAnnullamento = default)
    {
        var utente = await utenti.TrovaPerIdAsync(id, tokenAnnullamento);
        return utente is null
            ? EsitoReimpostazione.NonTrovato
            : await GeneraEInviaAsync(utente, baseUrl, tokenAnnullamento);
    }

    private async Task<EsitoReimpostazione> GeneraEInviaAsync(
        Utente utente,
        string baseUrl,
        CancellationToken tokenAnnullamento)
    {
        var tokenChiaro = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        utente.ResetTokenHash = HashToken(tokenChiaro);
        utente.ResetTokenScadenza = DateTime.UtcNow.Add(ValiditaLink);
        await unitOfWork.SalvaModificheAsync(tokenAnnullamento);

        var link = $"{baseUrl.TrimEnd('/')}/reimposta-password?token={Uri.EscapeDataString(tokenChiaro)}";
        var contenuto = "Ciao " + utente.Nome + ",\n\n"
            + "È stata richiesta la reimpostazione della password del tuo account AsCoach.\n"
            + "Apri questo link entro 30 minuti:\n\n"
            + link + "\n\n"
            + "Se non sei stato tu, ignora questa email: la password attuale resta valida.\n\n"
            + "A presto,\nDott. Alessandro";

        var inviata = await emailSender.InviaAsync(
            utente.Email,
            "Reimposta la password di AsCoach",
            contenuto,
            tokenAnnullamento: tokenAnnullamento);

        return new EsitoReimpostazione(true, link, inviata);
    }

    /// <summary>Valida il token monouso e imposta la nuova password.</summary>
    public async Task<bool> ReimpostaAsync(
        string? tokenChiaro,
        string nuovaPassword,
        CancellationToken tokenAnnullamento = default)
    {
        if (string.IsNullOrWhiteSpace(tokenChiaro) || !PasswordValida(nuovaPassword))
        {
            return false;
        }

        var utente = await utenti.TrovaPerResetTokenAsync(HashToken(tokenChiaro), tokenAnnullamento);
        if (utente is null)
        {
            return false;
        }

        ApplicaNuovaPassword(utente, nuovaPassword);
        await unitOfWork.SalvaModificheAsync(tokenAnnullamento);
        return true;
    }

    /// <summary>Cambio password da parte dell'utente autenticato, con verifica della password attuale.</summary>
    public async Task CambiaPasswordAsync(
        long id,
        string passwordAttuale,
        string nuovaPassword,
        CancellationToken tokenAnnullamento = default)
    {
        var utente = await utenti.TrovaPerIdAsync(id, tokenAnnullamento)
            ?? throw new InvalidOperationException("Utente non trovato");

        if (!passwordHasher.Verifica(passwordAttuale, utente.Password))
        {
            throw new InvalidOperationException("La password attuale non è corretta");
        }

        if (!PasswordValida(nuovaPassword))
        {
            throw new InvalidOperationException($"La nuova password deve contenere almeno {LunghezzaMinimaPassword} caratteri");
        }

        ApplicaNuovaPassword(utente, nuovaPassword);
        await unitOfWork.SalvaModificheAsync(tokenAnnullamento);
    }

    private void ApplicaNuovaPassword(Utente utente, string nuovaPassword)
    {
        utente.Password = passwordHasher.GeneraHash(nuovaPassword);
        utente.ResetTokenHash = null;
        utente.ResetTokenScadenza = null;
        utente.VersioneSicurezza++;
    }

    private static bool PasswordValida(string password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length >= LunghezzaMinimaPassword;

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
