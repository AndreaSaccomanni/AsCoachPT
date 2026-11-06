using AsCoachPT.Domain;

namespace AsCoachPT.Services;

public sealed record AppUser(long Id, string Nome, string Email, string Username, UserRole Role)
{
    public bool IsStaff => Role is UserRole.ADMIN or UserRole.PERSONAL_TRAINER;
}

public class RegisterRequest
{
    public string Nome { get; set; } = string.Empty;
    public string Cognome { get; set; } = string.Empty;
    public DateOnly? DataDiNascita { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
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
