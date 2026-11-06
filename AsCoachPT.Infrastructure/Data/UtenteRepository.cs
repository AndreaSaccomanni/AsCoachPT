using AsCoachPT.Application.Models;
using AsCoachPT.Application.Repositories;
using AsCoachPT.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AsCoachPT.Infrastructure.Data;

/// <summary>Accesso EF Core agli utenti.</summary>
public sealed class UtenteRepository(AsCoachDbContext contesto) : IUtenteRepository
{
    public Task<Utente?> TrovaPerIdAsync(long id, CancellationToken tokenAnnullamento = default) =>
        contesto.Utenti.FirstOrDefaultAsync(utente => utente.Id == id, tokenAnnullamento);

    public Task<Utente?> TrovaPerUsernameAsync(string username, CancellationToken tokenAnnullamento = default) =>
        contesto.Utenti.FirstOrDefaultAsync(utente => utente.Username == username, tokenAnnullamento);

    public Task<List<Utente>> ElencaAsync(CancellationToken tokenAnnullamento = default) =>
        contesto.Utenti.AsNoTracking().OrderBy(utente => utente.Id).ToListAsync(tokenAnnullamento);

    public Task<bool> EsisteEmailAsync(string email, CancellationToken tokenAnnullamento = default) =>
        contesto.Utenti.AnyAsync(utente => utente.Email == email, tokenAnnullamento);

    public Task<bool> EsisteUsernameAsync(string username, CancellationToken tokenAnnullamento = default) =>
        contesto.Utenti.AnyAsync(utente => utente.Username == username, tokenAnnullamento);

    public Task AggiungiAsync(Utente utente, CancellationToken tokenAnnullamento = default) =>
        contesto.Utenti.AddAsync(utente, tokenAnnullamento).AsTask();

    public void Rimuovi(Utente utente) => contesto.Utenti.Remove(utente);
}
