using AsCoachPT.Application.Repositories;
using AsCoachPT.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AsCoachPT.Infrastructure.Data;

/// <summary>Accesso EF Core agli indirizzi degli studi.</summary>
public sealed class IndirizzoRepository(AsCoachDbContext contesto) : IIndirizzoRepository
{
    public Task<List<Indirizzo>> ElencaAsync(bool includiDisabilitati = false, CancellationToken tokenAnnullamento = default) =>
        contesto.Indirizzi
            .AsNoTracking()
            .Where(indirizzo => includiDisabilitati || indirizzo.Attivo)
            .OrderBy(indirizzo => indirizzo.Id)
            .ToListAsync(tokenAnnullamento);

    public Task<Indirizzo?> TrovaPerIdAsync(long id, CancellationToken tokenAnnullamento = default) =>
        contesto.Indirizzi.FirstOrDefaultAsync(indirizzo => indirizzo.Id == id, tokenAnnullamento);

    public Task AggiungiAsync(Indirizzo indirizzo, CancellationToken tokenAnnullamento = default) =>
        contesto.Indirizzi.AddAsync(indirizzo, tokenAnnullamento).AsTask();

    public void Rimuovi(Indirizzo indirizzo) => contesto.Indirizzi.Remove(indirizzo);
}
