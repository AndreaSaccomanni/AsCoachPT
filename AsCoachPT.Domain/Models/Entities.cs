using System.ComponentModel.DataAnnotations.Schema;

namespace AsCoachPT.Domain.Models;

/// <summary>Persona registrata, con credenziali, ruolo e prenotazioni.</summary>
public class Utente
{
    // Chiave primaria.
    public long Id { get; set; }

    // Nome.
    public string Nome { get; set; } = string.Empty;

    // Cognome.
    public string Cognome { get; set; } = string.Empty;

    // Data di nascita, opzionale.
    public DateOnly? DataDiNascita { get; set; }

    // Email, univoca, usata per login e comunicazioni.
    public string Email { get; set; } = string.Empty;

    // Telefono.
    public string Telefono { get; set; } = string.Empty;

    // Campo legacy: per i nuovi utenti coincide con l'email.
    public string Username { get; set; } = string.Empty;

    // Hash BCrypt della password.
    public string Password { get; set; } = string.Empty;

    // Hash SHA-256 del token di reimpostazione, null se non attivo.
    public string? ResetTokenHash { get; set; }

    // Scadenza UTC del token di reimpostazione.
    public DateTime? ResetTokenScadenza { get; set; }

    // Incrementata a ogni cambio password: invalida i cookie emessi prima.
    public int VersioneSicurezza { get; set; } = 1;

    // Ruolo applicativo; null viene trattato come USER.
    public UserRole? Ruolo { get; set; }

    // Prenotazioni dell'utente, eliminate a cascata.
    public List<Prenotazione> Prenotazioni { get; set; } = new();
}

/// <summary>Tabella legacy dei ruoli, mantenuta per compatibilità con il database esistente.</summary>
public class Ruolo
{
    // Chiave primaria.
    public long Id { get; set; }

    // Nome del ruolo.
    public UserRole Name { get; set; }
}

/// <summary>Studio in cui il personal trainer riceve, con coordinate e giorni.</summary>
public class Indirizzo
{
    // Chiave primaria.
    public long Id { get; set; }

    // Via.
    public string Via { get; set; } = string.Empty;

    // Numero civico.
    public string NumeroCivico { get; set; } = string.Empty;

    // Città.
    public string Citta { get; set; } = string.Empty;

    // Provincia.
    public string Provincia { get; set; } = string.Empty;

    // Latitudine, usata dalla mappa.
    public double? Latitudine { get; set; }

    // Longitudine, usata dalla mappa.
    public double? Longitudine { get; set; }

    // Nome con cui lo studio appare sul sito.
    public string NomeStudio { get; set; } = string.Empty;

    // Giorni separati da virgola, come salvati a database.
    public string? GiorniDisponibili { get; set; }

    /// <summary>
    /// Falso quando lo studio è disabilitato: non compare nelle prenotazioni
    /// e i suoi giorni tornano liberi per un altro indirizzo.
    /// </summary>
    public bool Attivo { get; set; } = true;

    // Giorni già separati e ripuliti, calcolati da GiorniDisponibili.
    [NotMapped]
    public List<string> Giorni => string.IsNullOrWhiteSpace(GiorniDisponibili)
        ? new List<string>()
        : GiorniDisponibili.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    // Indirizzo completo pronto per essere mostrato.
    public string IndirizzoCompleto => $"{Via} {NumeroCivico}, {Citta} ({Provincia})";
}

/// <summary>Servizio prenotabile; le sottoclassi aggiungono il tipo specifico.</summary>
public abstract class Servizio
{
    // Chiave primaria condivisa con le sottoclassi.
    public long Id { get; set; }

    // Durata in minuti, usata per gli orari e i controlli di sovrapposizione.
    public int Durata { get; set; }

    // Nome leggibile calcolato dal tipo specifico.
    [NotMapped]
    public abstract string NomeServizio { get; }
}

/// <summary>Consulenza, sottotipo di servizio.</summary>
public class Consulenza : Servizio
{
    // Tipo di consulenza.
    public TipoConsulenza? TipoConsulenza { get; set; }

    // Nome mostrato nelle prenotazioni.
    public override string NomeServizio => $"Consulenza {TipoConsulenza}";
}

/// <summary>Massaggio, sottotipo di servizio.</summary>
public class Massaggio : Servizio
{
    // Tipo di massaggio.
    public TipoMassaggio? TipoMassaggio { get; set; }

    // Nome mostrato nelle prenotazioni.
    public override string NomeServizio => $"Massaggio {TipoMassaggio}";
}

/// <summary>Appuntamento che collega utente, servizio e studio in una data e ora.</summary>
public class Prenotazione
{
    // Chiave primaria.
    public long Id { get; set; }

    // Chiave esterna dell'utente.
    public long UtenteId { get; set; }

    // Utente che ha prenotato.
    public Utente Utente { get; set; } = null!;

    // Inizio dell'appuntamento, ora locale senza fuso.
    public DateTime DataOra { get; set; }

    // Chiave esterna del servizio.
    public long ServizioId { get; set; }

    // Servizio prenotato, da cui deriva la durata.
    public Servizio Servizio { get; set; } = null!;

    // Chiave esterna dello studio.
    public long IndirizzoId { get; set; }

    // Studio dell'appuntamento.
    public Indirizzo Indirizzo { get; set; } = null!;

    // Note libere dell'utente.
    public string? Note { get; set; }
}

/// <summary>Avviso per lo staff generato quando un utente modifica o annulla una prenotazione.</summary>
public class NotificaPrenotazione
{
    // Chiave primaria.
    public long Id { get; set; }

    // Prenotazione collegata, se esiste ancora.
    public long? PrenotazioneId { get; set; }

    // Tipo di evento: MODIFICA o ANNULLAMENTO.
    public string Tipo { get; set; } = string.Empty;

    // Testo mostrato allo staff.
    public string Messaggio { get; set; } = string.Empty;

    // Istante UTC di creazione.
    public DateTime CreataIl { get; set; } = DateTime.UtcNow;

    // Falso finché lo staff non la segna come letta.
    public bool Letta { get; set; }
}
