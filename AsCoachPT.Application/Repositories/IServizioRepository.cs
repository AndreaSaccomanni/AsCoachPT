using AsCoachPT.Domain.Models;

namespace AsCoachPT.Application.Repositories;

/// <summary>Accesso al catalogo dei servizi prenotabili.</summary>
public interface IServizioRepository
{
    Task<List<Servizio>> ElencaAsync(CancellationToken tokenAnnullamento = default);
    Task<Servizio?> TrovaPerIdAsync(long id, CancellationToken tokenAnnullamento = default);
}
