using AsCoachPT.Domain.Models;

namespace AsCoachPT.Application.Repositories;

/// <summary>Accesso ai dati delle prenotazioni.</summary>
public interface IPrenotazioneRepository
{
    Task<Prenotazione?> TrovaPerIdAsync(long id, CancellationToken tokenAnnullamento = default);
    Task<List<Prenotazione>> ElencaFutureAsync(CancellationToken tokenAnnullamento = default);
    Task<List<Prenotazione>> ElencaFuturePerUtenteAsync(long utenteId, CancellationToken tokenAnnullamento = default);
    Task<List<Prenotazione>> ElencaPerIndirizzoAsync(long indirizzoId, CancellationToken tokenAnnullamento = default);
    Task<List<Prenotazione>> ElencaDelGiornoAsync(DateOnly data, CancellationToken tokenAnnullamento = default);
    Task<List<Prenotazione>> ElencaConServizioAsync(CancellationToken tokenAnnullamento = default);
    Task AggiungiAsync(Prenotazione prenotazione, CancellationToken tokenAnnullamento = default);
    void Rimuovi(Prenotazione prenotazione);
    Task AggiungiNotificaAsync(NotificaPrenotazione notifica, CancellationToken tokenAnnullamento = default);
    Task<List<NotificaPrenotazione>> ElencaNotificheNonLetteAsync(CancellationToken tokenAnnullamento = default);
    Task SegnaNotificheLetteAsync(CancellationToken tokenAnnullamento = default);
}
