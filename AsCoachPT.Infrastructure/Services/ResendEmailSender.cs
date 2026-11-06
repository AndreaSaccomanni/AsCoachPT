using System.Net.Http.Headers;
using System.Net.Http.Json;
using AsCoachPT.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsCoachPT.Infrastructure.Services;

/// <summary>Configurazione dell'invio email tramite API Resend.</summary>
public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    public string ApiKey { get; set; } = string.Empty;
    public string From { get; set; } = "onboarding@resend.dev";
}

/// <summary>Invia email tramite l'API HTTP di Resend: funziona anche dove SMTP è bloccato.</summary>
public sealed class ResendEmailSender(
    HttpClient client,
    IOptions<ResendOptions> opzioni,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    private readonly ResendOptions _impostazioni = opzioni.Value;

    public async Task<bool> InviaAsync(
        string destinatario,
        string oggetto,
        string contenuto,
        string? rispostaA = null,
        CancellationToken tokenAnnullamento = default)
    {
        if (string.IsNullOrWhiteSpace(_impostazioni.ApiKey))
        {
            logger.LogWarning("Resend non configurato: email a {Destinatario} non inviata", destinatario);
            return false;
        }

        var corpo = new Dictionary<string, object>
        {
            ["from"] = _impostazioni.From,
            ["to"] = new[] { destinatario },
            ["subject"] = oggetto,
            ["text"] = contenuto
        };

        if (!string.IsNullOrWhiteSpace(rispostaA))
        {
            corpo["reply_to"] = rispostaA;
        }

        using var richiesta = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(corpo)
        };
        richiesta.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _impostazioni.ApiKey);

        try
        {
            using var risposta = await client.SendAsync(richiesta, tokenAnnullamento);
            if (risposta.IsSuccessStatusCode)
            {
                logger.LogInformation("Email Resend inviata a {Destinatario}", destinatario);
                return true;
            }

            var dettaglio = await risposta.Content.ReadAsStringAsync(tokenAnnullamento);
            logger.LogError("Resend ha rifiutato l'email a {Destinatario}: {Status} {Dettaglio}",
                destinatario, (int)risposta.StatusCode, dettaglio);
            return false;
        }
        catch (Exception eccezione)
        {
            logger.LogError(eccezione, "Errore nell'invio Resend a {Destinatario}", destinatario);
            return false;
        }
    }
}
