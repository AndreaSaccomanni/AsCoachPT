using AsCoachPT.Application.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AsCoachPT.Infrastructure.Services;

/// <summary>Impostazioni SMTP lette da configurazione o User Secrets.</summary>
public sealed class MailOptions
{
    public const string SectionName = "Mail";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = "alessandrosaccomanni.pt@gmail.com";
    public string Security { get; set; } = "StartTls";
}

/// <summary>Invia email di testo tramite MailKit.</summary>
public sealed class SmtpEmailSender(
    IOptions<MailOptions> opzioni,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly MailOptions _impostazioni = opzioni.Value;

    /// <summary>Invia il messaggio; restituisce false se il server non è configurato o rifiuta l'invio.</summary>
    public async Task<bool> InviaAsync(
        string destinatario,
        string oggetto,
        string contenuto,
        string? rispostaA = null,
        CancellationToken tokenAnnullamento = default)
    {
        if (!ConfigurazioneCompleta)
        {
            logger.LogWarning("SMTP non configurato: email a {Destinatario} non inviata", destinatario);
            return false;
        }

        try
        {
            var messaggio = new MimeMessage();
            messaggio.From.Add(MailboxAddress.Parse(_impostazioni.From));
            messaggio.To.Add(MailboxAddress.Parse(destinatario));
            messaggio.Subject = oggetto;

            if (!string.IsNullOrWhiteSpace(rispostaA))
            {
                messaggio.ReplyTo.Add(MailboxAddress.Parse(rispostaA));
            }

            messaggio.Body = new TextPart("plain") { Text = contenuto };

            using var client = new SmtpClient { Timeout = 15000 };
            await client.ConnectAsync(_impostazioni.Host, _impostazioni.Port, OpzioniSocket, tokenAnnullamento);

            if (!string.IsNullOrWhiteSpace(_impostazioni.Username))
            {
                await client.AuthenticateAsync(_impostazioni.Username, _impostazioni.Password, tokenAnnullamento);
            }

            await client.SendAsync(messaggio, tokenAnnullamento);
            await client.DisconnectAsync(true, tokenAnnullamento);

            logger.LogInformation("Mail inviata correttamente a: {Destinatario}", destinatario);
            return true;
        }
        catch (Exception eccezione)
        {
            logger.LogError(eccezione, "Errore nell'invio della mail a {Destinatario}", destinatario);
            return false;
        }
    }

    private bool ConfigurazioneCompleta => !string.IsNullOrWhiteSpace(_impostazioni.Host);

    private SecureSocketOptions OpzioniSocket => _impostazioni.Security switch
    {
        "None" => SecureSocketOptions.None,
        "SslOnConnect" => SecureSocketOptions.SslOnConnect,
        "Auto" => SecureSocketOptions.Auto,
        _ => SecureSocketOptions.StartTls
    };
}
