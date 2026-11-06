using AsCoachPT.Data;
using AsCoachPT.Domain;
using Microsoft.EntityFrameworkCore;

namespace AsCoachPT.Services;

public class PrenotazioneService(AsCoachDbContext db, MailService mail)
{
    private const int PassoMinuti = 15;
    private static readonly TimeOnly Apertura = new(9, 0);
    private static readonly TimeOnly Chiusura = new(20, 30);
    private static readonly TimeOnly ChiusuraSabato = new(13, 0);

    public List<Prenotazione> GetAllFuture()
    {
        var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        return BaseQuery()
            .Where(p => p.DataOra > now)
            .OrderBy(p => p.DataOra)
            .ToList();
    }

    public List<Prenotazione> GetForUtente(long utenteId)
    {
        var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        return BaseQuery()
            .Where(p => p.UtenteId == utenteId && p.DataOra > now)
            .OrderBy(p => p.DataOra)
            .ToList();
    }

    public List<Prenotazione> GetByIndirizzo(long indirizzoId) =>
        BaseQuery().Where(p => p.IndirizzoId == indirizzoId).ToList();

    public Prenotazione GetById(long id) =>
        BaseQuery().FirstOrDefault(p => p.Id == id)
        ?? throw new InvalidOperationException("Prenotazione non trovata ️ ️");

    public async Task<Prenotazione> CreaAsync(long? utenteIdSelezionato, long servizioId, DateTime dataOra, long indirizzoId, string? note, AppUser actor)
    {
        var servizio = db.Servizi.FirstOrDefault(s => s.Id == servizioId)
            ?? throw new InvalidOperationException(" Servizio non trovato! ");

        Utente utentePrenotazione;
        if (actor.IsStaff && utenteIdSelezionato is not null)
        {
            utentePrenotazione = db.Utenti.FirstOrDefault(u => u.Id == utenteIdSelezionato)
                ?? throw new InvalidOperationException("Utente per la prenotazione non trovato!");
        }
        else
        {
            utentePrenotazione = db.Utenti.FirstOrDefault(u => u.Id == actor.Id)
                ?? throw new InvalidOperationException("Utente per la prenotazione non trovato!");
        }

        var indirizzo = db.Indirizzi.FirstOrDefault(i => i.Id == indirizzoId)
            ?? throw new InvalidOperationException("Indirizzo non trovato");

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

        if (IsSovrapposizione(dataOra, finePrenotazione, null))
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

        db.Prenotazioni.Add(prenotazione);
        await db.SaveChangesAsync();

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

        await mail.InviaEmailAsync(utentePrenotazione.Email, "Conferma Prenotazione - " + servizio.NomeServizio, contenuto);

        return prenotazione;
    }

    public async Task DeleteAsync(long id, AppUser actor)
    {
        var prenotazione = GetById(id);

        if (prenotazione.UtenteId == actor.Id || actor.IsStaff)
        {
            var contenuto = "Ciao " + prenotazione.Utente.Nome + ",\n\n"
                + "La tua prenotazione per il servizio '" + prenotazione.Servizio.NomeServizio + "' prevista per il " + prenotazione.DataOra.ToString("dd/MM/yyyy") +
                " alle: " + prenotazione.DataOra.ToString("HH:mm") + " a " + prenotazione.Indirizzo.Citta + ", " + prenotazione.Indirizzo.Via + " " +
                prenotazione.Indirizzo.NumeroCivico + " - " + prenotazione.Indirizzo.NomeStudio + " è stata cancellata con successo.\n\n"
                + "Se la cancellazione è avvenuta per errore o desideri prenotare un nuovo appuntamento,\n"
                + "puoi farlo direttamente accedendo alla tua area personale o contattandomi.\n\n"
                + "Grazie!\n"
                + "A presto, cordiali saluti,\n\nDott.Alessandro";

            db.Prenotazioni.Remove(prenotazione);
            await db.SaveChangesAsync();

            await mail.InviaEmailAsync(
                prenotazione.Utente.Email,
                "Cancellazione Prenotazione - " + prenotazione.Servizio.NomeServizio,
                contenuto);

            return;
        }

        throw new UnauthorizedAccessException("Non sei autorizzato a cancellare questa prenotazione");
    }

    public async Task<Prenotazione> UpdateAsync(long id, UpdatePrenotazioneRequest request, AppUser actor)
    {
        var prenotazione = GetById(id);

        if (prenotazione.UtenteId != actor.Id && !actor.IsStaff)
        {
            throw new UnauthorizedAccessException("Non puoi modificare questa prenotazione ");
        }

        if (request.DataOraPrenotazione is not null)
        {
            var servizioId = request.ServizioId
                ?? throw new InvalidOperationException("Per modificare la data è necessario indicare il servizio");

            var servizio = db.Servizi.FirstOrDefault(s => s.Id == servizioId)
                ?? throw new InvalidOperationException("Servizio non trovato!");

            var dataOra = DateTime.SpecifyKind(request.DataOraPrenotazione.Value, DateTimeKind.Unspecified);
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

            if (IsSovrapposizione(dataOra, finePrenotazione, prenotazione.Id))
            {
                throw new InvalidOperationException("Impossibile creare una prenotazione per l'ora e la data selezionata, c'è gia un altro appuntamento prenotato ");
            }

            prenotazione.Servizio = servizio;
            prenotazione.DataOra = dataOra;
        }
        else if (request.ServizioId is not null)
        {
            prenotazione.Servizio = db.Servizi.FirstOrDefault(s => s.Id == request.ServizioId)
                ?? throw new InvalidOperationException(" ️Servizio non trovato! ");
        }

        if (request.UtenteId is not null)
        {
            prenotazione.Utente = db.Utenti.FirstOrDefault(u => u.Id == request.UtenteId)
                ?? throw new InvalidOperationException("Utente non trovato! ");
        }

        if (request.Note is not null)
        {
            prenotazione.Note = request.Note;
        }

        if (request.IndirizzoId is not null)
        {
            prenotazione.Indirizzo = db.Indirizzi.FirstOrDefault(i => i.Id == request.IndirizzoId)
                ?? throw new InvalidOperationException("Indirizzo non trovato");
        }

        await db.SaveChangesAsync();

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

        await mail.InviaEmailAsync(
            prenotazione.Utente.Email,
            "Aggiornamento Prenotazione - " + prenotazione.Servizio.NomeServizio,
            contenuto);

        return prenotazione;
    }

    public List<DateTime> GetOrariDisponibili(DateOnly data, long servizioId)
    {
        var servizio = db.Servizi.FirstOrDefault(s => s.Id == servizioId)
            ?? throw new InvalidOperationException("Servizio non trovato!");

        var inizioGiorno = data.ToDateTime(TimeOnly.MinValue);
        var fineGiorno = data.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var prenotazioni = db.Prenotazioni.AsNoTracking()
            .Include(p => p.Servizio)
            .Where(p => p.DataOra >= inizioGiorno && p.DataOra < fineGiorno)
            .ToList();

        var orariOccupati = new HashSet<TimeOnly>();
        foreach (var prenotazione in prenotazioni)
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

    private bool IsSovrapposizione(DateTime start, DateTime end, long? prenotazioneId)
    {
        var esistenti = db.Prenotazioni.AsNoTracking().Include(p => p.Servizio).ToList();

        foreach (var prenotazione in esistenti)
        {
            if (prenotazioneId is not null && prenotazione.Id == prenotazioneId)
            {
                continue;
            }

            var inizioEsistente = prenotazione.DataOra;
            var fineEsistente = inizioEsistente.AddMinutes(prenotazione.Servizio.Durata);

            if (start < fineEsistente && end > inizioEsistente)
            {
                return true;
            }
        }

        return false;
    }

    private IQueryable<Prenotazione> BaseQuery() =>
        db.Prenotazioni
            .Include(p => p.Utente)
            .Include(p => p.Servizio)
            .Include(p => p.Indirizzo);
}
