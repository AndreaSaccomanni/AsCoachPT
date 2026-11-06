using AsCoachPT.Application.Repositories;
using AsCoachPT.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AsCoachPT.Infrastructure.Data;

/// <summary>Accesso EF Core al catalogo dei servizi.</summary>
public sealed class ServizioRepository(AsCoachDbContext contesto) : IServizioRepository
{
    public Task<List<Servizio>> ElencaAsync(CancellationToken tokenAnnullamento = default) =>
        contesto.Servizi.AsNoTracking().OrderBy(servizio => servizio.Id).ToListAsync(tokenAnnullamento);

    public Task<Servizio?> TrovaPerIdAsync(long id, CancellationToken tokenAnnullamento = default) =>
        contesto.Servizi.FirstOrDefaultAsync(servizio => servizio.Id == id, tokenAnnullamento);
}
