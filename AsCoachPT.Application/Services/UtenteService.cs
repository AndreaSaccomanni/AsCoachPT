using AsCoachPT.Application.Models;
using AsCoachPT.Application.Repositories;
using AsCoachPT.Application.Services;
using AsCoachPT.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AsCoachPT.Application.Services;

/// <summary>Gestisce registrazione, autenticazione e anagrafica degli utenti.</summary>
public sealed class UtenteService(
    IUtenteRepository utenti,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    ILogger<UtenteService> logger)
{
    /// <summary>Registra un nuovo utente e invia l'email di benvenuto.</summary>
    public async Task<Utente> RegistraAsync(RegisterRequest richiesta, CancellationToken tokenAnnullamento = default)
    {
        if (await utenti.EsisteEmailAsync(richiesta.Email, tokenAnnullamento))
        {
            throw new InvalidOperationException("Email già presente nel sistema");
        }

        if (await utenti.EsisteUsernameAsync(richiesta.Username, tokenAnnullamento))
        {
            throw new InvalidOperationException("Username già presente nel sistema");
        }

        var ruolo = UserRole.USER;
        if (!string.IsNullOrWhiteSpace(richiesta.Ruolo) &&
            Enum.TryParse<UserRole>(richiesta.Ruolo, ignoreCase: true, out var ruoloRichiesto))
        {
            ruolo = ruoloRichiesto;
        }

        var utente = new Utente
        {
            Nome = richiesta.Nome,
            Cognome = richiesta.Cognome,
            DataDiNascita = richiesta.DataDiNascita,
            Email = richiesta.Email,
            Username = richiesta.Username,
            Password = passwordHasher.GeneraHash(richiesta.Password),
            Ruolo = ruolo
        };

        await utenti.AggiungiAsync(utente, tokenAnnullamento);
        await unitOfWork.SalvaModificheAsync(tokenAnnullamento);

        var contenuto = "Ciao " + utente.Nome + ",\n\n"
            + "La registrazione è avvenuta  con successo!\n\n"
            + "Ora potrai effetturare prenotazioni per i seguenti servizi:\n\n"
            + "🎗️ CONSULENZA INIZIALE: Durante il primo incontro analizziamo insieme il tuo stato fisico attuale, i tuoi obiettivi e le tue abitudini di allenamento" + "\n"
            + "💆‍♂️ MASSAGGIO RILASSANTE: Un massaggio della durata di un'ora che aiuta a sciogliere le tensioni accumulate e favorisce un senso di benessere generale" + "\n"
            + "🏃 MASSAGGIO DECONTRATTURANTE: Un massaggio che aiuta a eliminare l'acido lattico e migliora la circolazione per un recupero più rapido dopo l'allenamento." + "\n\n"
            + "Se hai bisogno di altre informazioni non esitare a contattarmi:\n"
            + "📞 3772082714\n"
            + "📩 alessandrosaccomanni.pt@gmail.com\n\n"
            + "Grazie!\n"
            + "A presto, cordiali saluti,\n\nDott.Alessandro";

        var inviata = await emailSender.InviaAsync(utente.Email, "Registrazione avvenuta con successo", contenuto, tokenAnnullamento: tokenAnnullamento);
        if (!inviata)
        {
            logger.LogWarning("Email di benvenuto non inviata per {Utente}", utente.Username);
        }

        return utente;
    }

    /// <summary>Verifica username e password; restituisce null se le credenziali non sono valide.</summary>
    public async Task<Utente?> VerificaCredenzialiAsync(string username, string password, CancellationToken tokenAnnullamento = default)
    {
        var utente = await utenti.TrovaPerUsernameAsync(username, tokenAnnullamento);
        if (utente is null || !passwordHasher.Verifica(password, utente.Password))
        {
            return null;
        }

        return utente;
    }

    /// <summary>Restituisce tutti gli utenti in ordine di identificativo.</summary>
    public Task<List<Utente>> ElencaAsync(CancellationToken tokenAnnullamento = default) =>
        utenti.ElencaAsync(tokenAnnullamento);

    /// <summary>Recupera un utente o segnala che non esiste.</summary>
    public async Task<Utente> TrovaPerIdAsync(long id, CancellationToken tokenAnnullamento = default) =>
        await utenti.TrovaPerIdAsync(id, tokenAnnullamento) ?? throw new InvalidOperationException("Utente non trovato!⚠️");

    /// <summary>Aggiorna i dati anagrafici dell'utente, ricodificando la password se indicata.</summary>
    public async Task<Utente> AggiornaAsync(long id, Utente modifiche, CancellationToken tokenAnnullamento = default)
    {
        var utente = await TrovaPerIdAsync(id, tokenAnnullamento);

        if (!string.IsNullOrWhiteSpace(modifiche.Nome)) utente.Nome = modifiche.Nome;
        if (!string.IsNullOrWhiteSpace(modifiche.Cognome)) utente.Cognome = modifiche.Cognome;
        if (modifiche.DataDiNascita is not null) utente.DataDiNascita = modifiche.DataDiNascita;
        if (!string.IsNullOrWhiteSpace(modifiche.Username)) utente.Username = modifiche.Username;
        if (!string.IsNullOrWhiteSpace(modifiche.Email)) utente.Email = modifiche.Email;
        if (!string.IsNullOrWhiteSpace(modifiche.Password)) utente.Password = passwordHasher.GeneraHash(modifiche.Password);
        if (modifiche.Ruolo is not null) utente.Ruolo = modifiche.Ruolo;

        try
        {
            await unitOfWork.SalvaModificheAsync(tokenAnnullamento);
        }
        catch (Exception eccezione) when (eccezione.GetType().Name == "DbUpdateException")
        {
            throw new InvalidOperationException("Username o email già presenti nel sistema");
        }

        return utente;
    }

    /// <summary>Elimina l'utente e le sue prenotazioni.</summary>
    public async Task EliminaAsync(long id, CancellationToken tokenAnnullamento = default)
    {
        var utente = await TrovaPerIdAsync(id, tokenAnnullamento);
        utenti.Rimuovi(utente);
        await unitOfWork.SalvaModificheAsync(tokenAnnullamento);
    }
}
