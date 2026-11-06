using System.ComponentModel.DataAnnotations.Schema;

namespace AsCoachPT.Domain.Models;

public class Utente
{
    public long Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cognome { get; set; } = string.Empty;
    public DateOnly? DataDiNascita { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public UserRole? Ruolo { get; set; }
    public List<Prenotazione> Prenotazioni { get; set; } = new();

    public bool IsStaff => Ruolo is UserRole.ADMIN or UserRole.PERSONAL_TRAINER;
}

public class Ruolo
{
    public long Id { get; set; }
    public UserRole Name { get; set; }
}

public class Indirizzo
{
    public long Id { get; set; }
    public string Via { get; set; } = string.Empty;
    public string NumeroCivico { get; set; } = string.Empty;
    public string Citta { get; set; } = string.Empty;
    public string Provincia { get; set; } = string.Empty;
    public double? Latitudine { get; set; }
    public double? Longitudine { get; set; }
    public string NomeStudio { get; set; } = string.Empty;
    public string? GiorniDisponibili { get; set; }

    [NotMapped]
    public List<string> Giorni => string.IsNullOrWhiteSpace(GiorniDisponibili)
        ? new List<string>()
        : GiorniDisponibili.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public string IndirizzoCompleto => $"{Via} {NumeroCivico}, {Citta} ({Provincia})";
}

public abstract class Servizio
{
    public long Id { get; set; }
    public int Durata { get; set; }

    [NotMapped]
    public abstract string NomeServizio { get; }
}

public class Consulenza : Servizio
{
    public TipoConsulenza? TipoConsulenza { get; set; }

    public override string NomeServizio => $"Consulenza {TipoConsulenza}";

    public void ImpostaDurata()
    {
        Durata = TipoConsulenza switch
        {
            AsCoachPT.Domain.Models.TipoConsulenza.VALUTAZIONE_INIZIALE => 30,
            AsCoachPT.Domain.Models.TipoConsulenza.MIGLIORAMENTO_SCHEDA => 15,
            _ => throw new ArgumentException($"Tipo di consulenza non valido: {TipoConsulenza}")
        };
    }
}

public class Massaggio : Servizio
{
    public TipoMassaggio? TipoMassaggio { get; set; }

    public override string NomeServizio => $"Massaggio {TipoMassaggio}";

    public void ImpostaDurata()
    {
        Durata = TipoMassaggio switch
        {
            AsCoachPT.Domain.Models.TipoMassaggio.RILASSANTE => 60,
            AsCoachPT.Domain.Models.TipoMassaggio.DECONTRATTURANTE_SPORTIVO => 30,
            AsCoachPT.Domain.Models.TipoMassaggio.APPLICAZIONE_TAPE => 15,
            _ => throw new ArgumentException($"Tipo di massaggio non valido: {TipoMassaggio}")
        };
    }
}

public class Prenotazione
{
    public long Id { get; set; }
    public long UtenteId { get; set; }
    public Utente Utente { get; set; } = null!;
    public DateTime DataOra { get; set; }
    public long ServizioId { get; set; }
    public Servizio Servizio { get; set; } = null!;
    public long IndirizzoId { get; set; }
    public Indirizzo Indirizzo { get; set; } = null!;
    public string? Note { get; set; }
}
