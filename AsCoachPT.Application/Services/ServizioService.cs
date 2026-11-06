using AsCoachPT.Application.Repositories;
using AsCoachPT.Domain.Models;

namespace AsCoachPT.Application.Services;

/// <summary>Espone il catalogo dei servizi prenotabili.</summary>
public sealed class ServizioService(IServizioRepository servizi)
{
    /// <summary>Restituisce tutti i servizi in ordine di inserimento.</summary>
    public Task<List<Servizio>> ElencaAsync(CancellationToken tokenAnnullamento = default) =>
        servizi.ElencaAsync(tokenAnnullamento);
}
