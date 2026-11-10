using AsCoachPT.Domain.Models;

namespace AsCoachPT.Application.Models;

/// <summary>Utente autenticato usato dalla logica applicativa.</summary>
public sealed record AppUser(long Id, string Nome, string Email, string Username, UserRole Role)
{
    public bool IsStaff => Role is UserRole.ADMIN or UserRole.PERSONAL_TRAINER;

    /// <summary>Crea l'utente applicativo a partire dall'entità di dominio.</summary>
    public static AppUser DaUtente(Utente utente) => new(
        utente.Id,
        utente.Nome,
        utente.Email,
        utente.Username,
        utente.Ruolo ?? UserRole.USER);
}

public class RegisterRequest
{
    public string Nome { get; set; } = string.Empty;
    public string Cognome { get; set; } = string.Empty;
    public DateOnly? DataDiNascita { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Ruolo { get; set; }
}

public class UpdatePrenotazioneRequest
{
    public DateTime? DataOraPrenotazione { get; set; }
    public long? ServizioId { get; set; }
    public long? UtenteId { get; set; }
    public long? IndirizzoId { get; set; }
    public string? Note { get; set; }
}

public class FormInformazioni
{
    public string Nome { get; set; } = string.Empty;
    public string Cognome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Messaggio { get; set; } = string.Empty;
}
