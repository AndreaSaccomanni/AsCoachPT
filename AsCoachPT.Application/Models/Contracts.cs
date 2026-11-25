using AsCoachPT.Domain.Models;

namespace AsCoachPT.Application.Models;

/// <summary>Utente autenticato usato dalla logica applicativa.</summary>
/// <param name="Id">Identificativo del record utente.</param>
/// <param name="Nome">Nome mostrato nelle email e nella navbar.</param>
/// <param name="Email">Indirizzo usato per le comunicazioni.</param>
/// <param name="Role">Ruolo che determina i permessi.</param>
public sealed record AppUser(long Id, string Nome, string Email, UserRole Role)
{
    /// <summary>Vero per amministratori e personal trainer, i ruoli con accesso allo staff.</summary>
    public bool IsStaff => Role is UserRole.ADMIN or UserRole.PERSONAL_TRAINER;
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
