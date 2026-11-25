namespace AsCoachPT.Domain.Models;

/// <summary>Ruolo applicativo dell'utente.</summary>
public enum UserRole
{
    /// <summary>Utente normale: prenota e gestisce i propri appuntamenti.</summary>
    USER,

    /// <summary>Amministratore: accesso completo a utenti, studi e prenotazioni.</summary>
    ADMIN,

    /// <summary>Personal trainer: gestisce utenti e prenotazioni.</summary>
    PERSONAL_TRAINER
}

/// <summary>Tipo di consulenza prenotabile.</summary>
public enum TipoConsulenza
{
    /// <summary>Prima valutazione dello stato fisico e degli obiettivi.</summary>
    VALUTAZIONE_INIZIALE,

    /// <summary>Aggiornamento della scheda di allenamento.</summary>
    MIGLIORAMENTO_SCHEDA
}

/// <summary>Tipo di massaggio prenotabile.</summary>
public enum TipoMassaggio
{
    /// <summary>Massaggio rilassante.</summary>
    RILASSANTE,

    /// <summary>Massaggio decontratturante sportivo.</summary>
    DECONTRATTURANTE_SPORTIVO,

    /// <summary>Applicazione di tape kinesiologico.</summary>
    APPLICAZIONE_TAPE
}
