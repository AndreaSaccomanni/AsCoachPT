using AsCoachPT.Application.Models;
using AsCoachPT.Application.Repositories;
using AsCoachPT.Application.Services;
using AsCoachPT.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AsCoachPT.Application.Services;

/// <summary>Gestisce le prenotazioni e le regole di disponibilità dello studio.</summary>
public sealed class PrenotazioneService(
    IUtenteRepository utenti,
    IServizioRepository servizi,
    IIndirizzoRepository indirizzi,
    IPrenotazioneRepository prenotazioni,
    IUnitOfWork unitOfWork,
    IEmailSender emailSender,
    ILogger<PrenotazioneService> logger)
{
    private const int PassoMinuti = 15;
    private static readonly TimeOnly Apertura = new(9, 0);
    private static readonly TimeOnly Chiusura = new(20, 30);
    private static readonly TimeOnly ChiusuraSabato = new(13, 0);

    /// <summary>Restituisce le prenotazioni future di tutti gli utenti (area staff).</summary>
    public Task<List<Prenotazione>> ElencaFutureAsync(CancellationToken tokenAnnullamento = default) =>
        prenotazioni.ElencaFutureAsync(tokenAnnullamento);

    /// <summary>Restituisce le prenotazioni future di un singolo utente.</summary>
    public Task<List<Prenotazione>> ElencaFuturePerUtenteAsync(long utenteId, CancellationToken tokenAnnullamento = default) =>
        prenotazioni.ElencaFuturePerUtenteAsync(utenteId, tokenAnnullamento);

    /// <summary>Restituisce le prenotazioni collegate a uno studio.</summary>
    public Task<List<Prenotazione>> ElencaPerIndirizzoAsync(long indirizzoId, CancellationToken tokenAnnullamento = default) =>
        prenotazioni.ElencaPerIndirizzoAsync(indirizzoId, tokenAnnullamento);

    /// <summary>Recupera una prenotazione o segnala che non esiste.</summary>
    public async Task<Prenotazione> TrovaPerIdAsync(long id, CancellationToken tokenAnnullamento = default) =>
        await prenotazioni.TrovaPerIdAsync(id, tokenAnnullamento)
        ?? throw new InvalidOperationException("Prenotazione non trovata ️ ️");

    /// <summary>Crea una prenotazione applicando orari, chiusure e controllo sovrapposizioni.</summary>
    public async Task<Prenotazione> CreaAsync(
        long? utenteIdSelezionato,
        long servizioId,
        DateTime dataOra,
        long indirizzoId,
        string? note,
        AppUser utenteAutenticato,
        CancellationToken tokenAnnullamento = default)
    {
        var servizio = await servizi.TrovaPerIdAsync(servizioId, tokenAnnullamento)
            ?? throw new InvalidOperationException(" Servizio non trovato! ");

        var utentePrenotazione = utenteAutenticato.IsStaff && utenteIdSelezionato is not null
            ? await utenti.TrovaPerIdAsync(utenteIdSelezionato.Value, tokenAnnullamento)
            : await utenti.TrovaPerIdAsync(utenteAutenticato.Id, tokenAnnullamento);

        if (utentePrenotazione is null)
        {
            throw new InvalidOperationException("Utente per la prenotazione non trovato!");
        }

        var indirizzo = await indirizzi.TrovaPerIdAsync(indirizzoId, tokenAnnullamento)
            ?? throw new InvalidOperationException("Indirizzo non trovato");

        if (!indirizzo.Attivo)
        {
            throw new InvalidOperationException("Lo studio selezionato non è attivo: scegli un altro indirizzo.");
        }

        dataOra = DateTime.SpecifyKind(dataOra, DateTimeKind.Unspecified);
        var durata = servizio.Durata;
        var finePrenotazione = dataOra.AddMinutes(durata);
        var oraInizio = TimeOnly.FromDateTime(dataOra);

        if (dataOra < DateTime.Now)
        {
            throw new InvalidOperationException(" Non è possibile prenotare un appuntamento per una data passata ");
        }

        if (dataOra.DayOfWeek == DayOfWeek.Sunday)
        {
            throw new InvalidOperationException(" Non si possono prenotare appuntamenti la domenica ");
        }

        VerificaGiornoDiRicevimento(indirizzo, dataOra);

        if (dataOra.DayOfWeek == DayOfWeek.Saturday && TimeOnly.FromDateTime(finePrenotazione) > ChiusuraSabato)
        {
            throw new InvalidOperationException("Il sabato gli appuntamenti devono concludersi entro le 13:00 ");
        }

        if (oraInizio < Apertura)
        {
            throw new InvalidOperationException(" Non è possibile prenotare appuntamenti prima delle 9:00");
        }

        if (TimeOnly.FromDateTime(finePrenotazione) > Chiusura)
        {
            throw new InvalidOperationException("Gli appuntamenti devono concludersi entro le 20:30 ");
        }

        if (await IsSovrapposizioneAsync(dataOra, finePrenotazione, null, tokenAnnullamento))
        {
            throw new InvalidOperationException("Impossibile creare una prenotazione per l'ora e la data selezionata, c'è gia un altro appuntamento prenotato");
        }

        var prenotazione = new Prenotazione
        {
            Utente = utentePrenotazione,
            Servizio = servizio,
            Indirizzo = indirizzo,
            DataOra = dataOra,
            Note = note
        };

        await prenotazioni.AggiungiAsync(prenotazione, tokenAnnullamento);
        await unitOfWork.SalvaModificheAsync(tokenAnnullamento);

        var contenuto = "Ciao " + utentePrenotazione.Nome + ",\n\n"
            + "La tua prenotazione per il servizio '" + servizio.NomeServizio + "' è stata confermata.\n\n"
            + "Dettagli:\n\n"
            + "📅 Data: " + dataOra.ToString("dd/MM/yyyy") + "\n"
            + "🕒 Orario: " + dataOra.ToString("HH:mm") + "\n"
            + "⌛ Durata: " + durata + " minuti\n"
            + "📍 Indirizzo: " + indirizzo.Citta + ",  " + indirizzo.Via + " " + indirizzo.NumeroCivico + " - " + indirizzo.NomeStudio + "\n"
            + (!string.IsNullOrWhiteSpace(note) ? "📝 Note: " + note + "\n\n" : "\n\n")
            + "Grazie!\n"
            + "A presto, cordiali saluti,\n\nDott.Alessandro";

        if (!await emailSender.InviaAsync(utentePrenotazione.Email, "Conferma Prenotazione - " + servizio.NomeServizio, contenuto, tokenAnnullamento: tokenAnnullamento))
        {
            logger.LogWarning("Conferma prenotazione non inviata per {Utente}", utentePrenotazione.Username);
        }

        return prenotazione;
    }

    /// <summary>Elimina una prenotazione se appartiene all'utente o se chi la cancella è staff.</summary>
    public async Task EliminaAsync(long id, AppUser utenteAutenticato, CancellationToken tokenAnnullamento = default)
    {
        var prenotazione = await TrovaPerIdAsync(id, tokenAnnullamento);

        if (prenotazione.UtenteId != utenteAutenticato.Id && !utenteAutenticato.IsStaff)
        {
            throw new UnauthorizedAccessException("Non sei autorizzato a cancellare questa prenotazione");
        }

        var contenuto = "Ciao " + prenotazione.Utente.Nome + ",\n\n"
            + "La tua prenotazione per il servizio '" + prenotazione.Servizio.NomeServizio + "' prevista per il " + prenotazione.DataOra.ToString("dd/MM/yyyy") +
            " alle: " + prenotazione.DataOra.ToString("HH:mm") + " a " + prenotazione.Indirizzo.Citta + ", " + prenotazione.Indirizzo.Via + " " +
            prenotazione.Indirizzo.NumeroCivico + " - " + prenotazione.Indirizzo.NomeStudio + " è stata cancellata con successo.\n\n"
            + "Se la cancellazione è avvenuta per errore o desideri prenotare un nuovo appuntamento,\n"
            + "puoi farlo direttamente accedendo alla tua area personale o contattandomi.\n\n"
            + "Grazie!\n"
            + "A presto, cordiali saluti,\n\nDott.Alessandro";

        prenotazioni.Rimuovi(prenotazione);
        await unitOfWork.SalvaModificheAsync(tokenAnnullamento);

        if (!await emailSender.InviaAsync(
                prenotazione.Utente.Email,
                "Cancellazione Prenotazione - " + prenotazione.Servizio.NomeServizio,
                contenuto,
                tokenAnnullamento: tokenAnnullamento))
        {
            logger.LogWarning("Email di cancellazione non inviata per {Utente}", prenotazione.Utente.Username);
        }
    }

    /// <summary>Aggiorna data, servizio, utente, note o indirizzo di una prenotazione.</summary>
    public async Task<Prenotazione> AggiornaAsync(
        long id,
        UpdatePrenotazioneRequest richiesta,
        AppUser utenteAutenticato,
        CancellationToken tokenAnnullamento = default)
    {
        var prenotazione = await TrovaPerIdAsync(id, tokenAnnullamento);

        if (prenotazione.UtenteId != utenteAutenticato.Id && !utenteAutenticato.IsStaff)
        {
            throw new UnauthorizedAccessException("Non puoi modificare questa prenotazione ");
        }

        if (richiesta.DataOraPrenotazione is not null)
        {
            var servizioId = richiesta.ServizioId
                ?? throw new InvalidOperationException("Per modificare la data è necessario indicare il servizio");

            var servizio = await servizi.TrovaPerIdAsync(servizioId, tokenAnnullamento)
                ?? throw new InvalidOperationException("Servizio non trovato!");

            var dataOra = DateTime.SpecifyKind(richiesta.DataOraPrenotazione.Value, DateTimeKind.Unspecified);
            var durata = servizio.Durata;
            var finePrenotazione = dataOra.AddMinutes(durata);
            var oraInizio = TimeOnly.FromDateTime(dataOra);

            if (dataOra < DateTime.Now)
            {
                throw new InvalidOperationException("Non è possibile prenotare un appuntamento per una data passata ");
            }

            if (dataOra.DayOfWeek == DayOfWeek.Sunday)
            {
                throw new InvalidOperationException("Non si possono prenotare appuntamenti la domenica ");
            }

            if (dataOra.DayOfWeek == DayOfWeek.Saturday && TimeOnly.FromDateTime(finePrenotazione) > ChiusuraSabato)
            {
                throw new InvalidOperationException("Il sabato gli appuntamenti devono concludersi entro le 13:00 ");
            }

            if (oraInizio < Apertura)
            {
                throw new InvalidOperationException(" Non è possibile prenotare appuntamenti prima delle 9:00");
            }

            if (TimeOnly.FromDateTime(finePrenotazione) > Chiusura)
            {
                throw new InvalidOperationException("Gli appuntamenti devono concludersi entro le 20:30 ");
            }

            if (await IsSovrapposizioneAsync(dataOra, finePrenotazione, prenotazione.Id, tokenAnnullamento))
            {
                throw new InvalidOperationException("Impossibile creare una prenotazione per l'ora e la data selezionata, c'è gia un altro appuntamento prenotato ");
            }

            prenotazione.Servizio = servizio;
            prenotazione.DataOra = dataOra;
        }
        else if (richiesta.ServizioId is not null)
        {
            prenotazione.Servizio = await servizi.TrovaPerIdAsync(richiesta.ServizioId.Value, tokenAnnullamento)
                ?? throw new InvalidOperationException(" ️Servizio non trovato! ");
        }

        if (richiesta.UtenteId is not null)
        {
            prenotazione.Utente = await utenti.TrovaPerIdAsync(richiesta.UtenteId.Value, tokenAnnullamento)
                ?? throw new InvalidOperationException("Utente non trovato! ");
        }

        if (richiesta.Note is not null)
        {
            prenotazione.Note = richiesta.Note;
        }

        if (richiesta.IndirizzoId is not null)
        {
            prenotazione.Indirizzo = await indirizzi.TrovaPerIdAsync(richiesta.IndirizzoId.Value, tokenAnnullamento)
                ?? throw new InvalidOperationException("Indirizzo non trovato");
        }

        // Data e studio sono ora quelli definitivi: controlliamo il giorno di ricevimento.
        if (!prenotazione.Indirizzo.Attivo)
        {
            throw new InvalidOperationException("Lo studio selezionato non è attivo: scegli un altro indirizzo.");
        }

        VerificaGiornoDiRicevimento(prenotazione.Indirizzo, prenotazione.DataOra);

        await unitOfWork.SalvaModificheAsync(tokenAnnullamento);

        var contenuto = "Ciao " + prenotazione.Utente.Nome + ",\n\n"
            + "La tua prenotazione per il servizio '" + prenotazione.Servizio.NomeServizio + "' è stata aggiornata.\n\n"
            + "Dettagli aggiornati:\n\n"
            + "📅 Data: " + prenotazione.DataOra.ToString("dd/MM/yyyy") + "\n"
            + "🕒 Orario: " + prenotazione.DataOra.ToString("HH:mm") + "\n"
            + "⌛ Durata: " + prenotazione.Servizio.Durata + " minuti\n"
            + "📍 Indirizzo: " + prenotazione.Indirizzo.Citta + ",  " + prenotazione.Indirizzo.Via + " " + prenotazione.Indirizzo.NumeroCivico + " - " + prenotazione.Indirizzo.NomeStudio + "\n"
            + (!string.IsNullOrWhiteSpace(prenotazione.Note) ? "📝 Note: " + prenotazione.Note + "\n\n" : "")
            + "Grazie!\n"
            + "A presto, cordiali saluti,\n\nDott.Alessandro";

        if (!await emailSender.InviaAsync(
                prenotazione.Utente.Email,
                "Aggiornamento Prenotazione - " + prenotazione.Servizio.NomeServizio,
                contenuto,
                tokenAnnullamento: tokenAnnullamento))
        {
            logger.LogWarning("Email di aggiornamento non inviata per {Utente}", prenotazione.Utente.Username);
        }

        return prenotazione;
    }

    /// <summary>Calcola gli orari liberi del giorno per un servizio, a passi di 15 minuti.</summary>
    public async Task<List<DateTime>> CalcolaOrariDisponibiliAsync(DateOnly data, long servizioId, CancellationToken tokenAnnullamento = default)
    {
        var servizio = await servizi.TrovaPerIdAsync(servizioId, tokenAnnullamento)
            ?? throw new InvalidOperationException("Servizio non trovato!");

        var prenotazioniDelGiorno = await prenotazioni.ElencaDelGiornoAsync(data, tokenAnnullamento);

        var orariOccupati = new HashSet<TimeOnly>();
        foreach (var prenotazione in prenotazioniDelGiorno)
        {
            var inizioOccupato = TimeOnly.FromDateTime(prenotazione.DataOra);
            var fineOccupato = inizioOccupato.AddMinutes(prenotazione.Servizio.Durata);

            while (inizioOccupato < fineOccupato)
            {
                orariOccupati.Add(inizioOccupato);
                inizioOccupato = inizioOccupato.AddMinutes(PassoMinuti);
            }
        }

        var durataServizio = servizio.Durata;
        var fineGiornata = data.DayOfWeek == DayOfWeek.Saturday ? ChiusuraSabato : Chiusura;

        var orariDisponibili = new List<DateTime>();
        var orarioCorrente = Apertura;

        while (orarioCorrente.AddMinutes(durataServizio) <= fineGiornata)
        {
            var occupato = false;
            for (var i = 0; i < durataServizio / PassoMinuti; i++)
            {
                if (orariOccupati.Contains(orarioCorrente.AddMinutes(i * PassoMinuti)))
                {
                    occupato = true;
                    break;
                }
            }

            if (!occupato)
            {
                orariDisponibili.Add(DateTime.SpecifyKind(data.ToDateTime(orarioCorrente), DateTimeKind.Unspecified));
            }

            orarioCorrente = orarioCorrente.AddMinutes(PassoMinuti);
        }

        return orariDisponibili;
    }

    /// <summary>
    /// Blocca la prenotazione se lo studio non riceve in quel giorno
    /// (i giorni di ricevimento sono configurati per ogni studio).
    /// </summary>
    private static void VerificaGiornoDiRicevimento(Indirizzo studio, DateTime dataOra)
    {
        if (GiorniStudio.StudioAperto(studio, dataOra.DayOfWeek))
        {
            return;
        }

        var giorni = GiorniStudio.ElencoLeggibile(GiorniStudio.DaStudi(new[] { studio }));
        throw new InvalidOperationException(
            $"Lo studio {studio.NomeStudio} non riceve il {GiorniStudio.NomeItaliano(dataOra.DayOfWeek)}. Giorni di ricevimento: {giorni}.");
    }

    private async Task<bool> IsSovrapposizioneAsync(DateTime inizio, DateTime fine, long? prenotazioneId, CancellationToken tokenAnnullamento)
    {
        var esistenti = await prenotazioni.ElencaConServizioAsync(tokenAnnullamento);

        foreach (var prenotazione in esistenti)
        {
            if (prenotazioneId is not null && prenotazione.Id == prenotazioneId)
            {
                continue;
            }

            var inizioEsistente = prenotazione.DataOra;
            var fineEsistente = inizioEsistente.AddMinutes(prenotazione.Servizio.Durata);

            if (inizio < fineEsistente && fine > inizioEsistente)
            {
                return true;
            }
        }

        return false;
    }
}
