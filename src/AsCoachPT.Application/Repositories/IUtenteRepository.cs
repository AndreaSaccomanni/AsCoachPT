using AsCoachPT.Domain.Models;

namespace AsCoachPT.Application.Repositories;

/// <summary>Accesso ai dati degli utenti registrati.</summary>
public interface IUtenteRepository
{
    Task<Utente?> TrovaPerIdAsync(long id, CancellationToken tokenAnnullamento = default);
    Task<Utente?> TrovaPerUsernameAsync(string username, CancellationToken tokenAnnullamento = default);
    Task<List<Utente>> ElencaAsync(CancellationToken tokenAnnullamento = default);
    Task<bool> EsisteEmailAsync(string email, CancellationToken tokenAnnullamento = default);
    Task<bool> EsisteUsernameAsync(string username, CancellationToken tokenAnnullamento = default);
    Task AggiungiAsync(Utente utente, CancellationToken tokenAnnullamento = default);
    void Rimuovi(Utente utente);
}
