namespace AsCoachPT.Application.Services;

/// <summary>Definisce l'invio delle email transazionali del sito.</summary>
public interface IEmailSender
{
    /// <summary>Invia un'email di testo semplice e restituisce se la consegna è riuscita.</summary>
    Task<bool> InviaAsync(
        string destinatario,
        string oggetto,
        string contenuto,
        string? rispostaA = null,
        CancellationToken tokenAnnullamento = default);
}
