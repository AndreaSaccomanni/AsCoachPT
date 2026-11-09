using AsCoachPT.Domain.Models;

namespace AsCoachPT.Application.Repositories;

/// <summary>Accesso ai dati degli studi e dei giorni disponibili.</summary>
public interface IIndirizzoRepository
{
    /// <summary>
    /// Elenca gli studi; di default solo quelli attivi, perché quelli disabilitati
    /// non devono comparire nelle prenotazioni.
    /// </summary>
    Task<List<Indirizzo>> ElencaAsync(bool includiDisabilitati = false, CancellationToken tokenAnnullamento = default);

    Task<Indirizzo?> TrovaPerIdAsync(long id, CancellationToken tokenAnnullamento = default);
    Task AggiungiAsync(Indirizzo indirizzo, CancellationToken tokenAnnullamento = default);
    void Rimuovi(Indirizzo indirizzo);
}
