using AsCoachPT.Application.Models;
using AsCoachPT.Application.Services;

namespace AsCoachPT.Application.Services;

/// <summary>Inoltra le richieste del modulo contatti all'indirizzo del personal trainer.</summary>
public sealed class ContattiService(IEmailSender emailSender)
{
    /// <summary>Invia il messaggio del visitatore e restituisce se l'invio è riuscito.</summary>
    public Task<bool> InviaRichiestaAsync(FormInformazioni richiesta, CancellationToken tokenAnnullamento = default)
    {
        var contenuto =
            "Nome: " + richiesta.Nome + "\n" +
            "Cognome: " + richiesta.Cognome + "\n" +
            "Messaggio: " + richiesta.Messaggio + "\n";

        return emailSender.InviaAsync(
            "alessandrosaccomanni.pt@gmail.com",
            "Richiesta informazioni",
            contenuto,
            rispostaA: richiesta.Email,
            tokenAnnullamento: tokenAnnullamento);
    }
}
