using AsCoachPT.Application.Repositories;

namespace AsCoachPT.Infrastructure.Data;

/// <summary>Implementa il salvataggio delle modifiche tramite il DbContext.</summary>
public sealed class UnitOfWork(AsCoachDbContext contesto) : IUnitOfWork
{
    public Task<int> SalvaModificheAsync(CancellationToken tokenAnnullamento = default) =>
        contesto.SaveChangesAsync(tokenAnnullamento);
}
