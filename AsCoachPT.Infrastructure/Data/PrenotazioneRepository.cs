using AsCoachPT.Application.Repositories;
using AsCoachPT.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AsCoachPT.Infrastructure.Data;

/// <summary>Accesso EF Core alle prenotazioni.</summary>
public sealed class PrenotazioneRepository(AsCoachDbContext contesto) : IPrenotazioneRepository
{
    public Task<Prenotazione?> TrovaPerIdAsync(long id, CancellationToken tokenAnnullamento = default) =>
        BaseQuery().FirstOrDefaultAsync(prenotazione => prenotazione.Id == id, tokenAnnullamento);

    public Task<List<Prenotazione>> ElencaFutureAsync(CancellationToken tokenAnnullamento = default)
    {
        var adesso = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        return BaseQuery()
            .Where(prenotazione => prenotazione.DataOra > adesso)
            .OrderBy(prenotazione => prenotazione.DataOra)
            .ToListAsync(tokenAnnullamento);
    }

    public Task<List<Prenotazione>> ElencaFuturePerUtenteAsync(long utenteId, CancellationToken tokenAnnullamento = default)
    {
        var adesso = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        return BaseQuery()
            .Where(prenotazione => prenotazione.UtenteId == utenteId && prenotazione.DataOra > adesso)
            .OrderBy(prenotazione => prenotazione.DataOra)
            .ToListAsync(tokenAnnullamento);
    }

    public Task<List<Prenotazione>> ElencaPerIndirizzoAsync(long indirizzoId, CancellationToken tokenAnnullamento = default) =>
        BaseQuery()
            .Where(prenotazione => prenotazione.IndirizzoId == indirizzoId)
            .ToListAsync(tokenAnnullamento);

    public Task<List<Prenotazione>> ElencaDelGiornoAsync(DateOnly data, CancellationToken tokenAnnullamento = default)
    {
        var inizioGiorno = data.ToDateTime(TimeOnly.MinValue);
        var fineGiorno = data.AddDays(1).ToDateTime(TimeOnly.MinValue);

        return contesto.Prenotazioni.AsNoTracking()
            .Include(prenotazione => prenotazione.Servizio)
            .Where(prenotazione => prenotazione.DataOra >= inizioGiorno && prenotazione.DataOra < fineGiorno)
            .ToListAsync(tokenAnnullamento);
    }

    public Task<List<Prenotazione>> ElencaConServizioAsync(CancellationToken tokenAnnullamento = default) =>
        contesto.Prenotazioni.AsNoTracking()
            .Include(prenotazione => prenotazione.Servizio)
            .ToListAsync(tokenAnnullamento);

    public Task AggiungiAsync(Prenotazione prenotazione, CancellationToken tokenAnnullamento = default) =>
        contesto.Prenotazioni.AddAsync(prenotazione, tokenAnnullamento).AsTask();

    public void Rimuovi(Prenotazione prenotazione) => contesto.Prenotazioni.Remove(prenotazione);

    public Task AggiungiNotificaAsync(NotificaPrenotazione notifica, CancellationToken tokenAnnullamento = default) =>
        contesto.NotifichePrenotazioni.AddAsync(notifica, tokenAnnullamento).AsTask();

    public Task<List<NotificaPrenotazione>> ElencaNotificheNonLetteAsync(CancellationToken tokenAnnullamento = default) =>
        contesto.NotifichePrenotazioni
            .AsNoTracking()
            .Where(notifica => !notifica.Letta)
            .OrderByDescending(notifica => notifica.CreataIl)
            .Take(20)
            .ToListAsync(tokenAnnullamento);

    public Task SegnaNotificheLetteAsync(CancellationToken tokenAnnullamento = default) =>
        contesto.NotifichePrenotazioni
            .Where(notifica => !notifica.Letta)
            .ExecuteUpdateAsync(aggiornamento => aggiornamento.SetProperty(notifica => notifica.Letta, true), tokenAnnullamento);

    private IQueryable<Prenotazione> BaseQuery() =>
        contesto.Prenotazioni
            .Include(prenotazione => prenotazione.Utente)
            .Include(prenotazione => prenotazione.Servizio)
            .Include(prenotazione => prenotazione.Indirizzo);
}
