using AsCoachPT.Domain.Models;

namespace AsCoachPT.Application.Repositories;

/// <summary>Accesso ai dati degli studi e dei giorni disponibili.</summary>
public interface IIndirizzoRepository
{
    Task<List<Indirizzo>> ElencaAsync(CancellationToken tokenAnnullamento = default);
    Task<Indirizzo?> TrovaPerIdAsync(long id, CancellationToken tokenAnnullamento = default);
    Task AggiungiAsync(Indirizzo indirizzo, CancellationToken tokenAnnullamento = default);
    void Rimuovi(Indirizzo indirizzo);
}
