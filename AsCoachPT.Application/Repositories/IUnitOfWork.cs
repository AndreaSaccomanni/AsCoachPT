namespace AsCoachPT.Application.Repositories;

/// <summary>Coordina il salvataggio delle modifiche su tutte le entità.</summary>
public interface IUnitOfWork
{
    /// <summary>Persiste le modifiche accumulate dai repository.</summary>
    Task<int> SalvaModificheAsync(CancellationToken tokenAnnullamento = default);
}
