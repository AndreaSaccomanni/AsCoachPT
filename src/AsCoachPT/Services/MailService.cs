using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AsCoachPT.Services;

public class MailOptions
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = "alessandrosaccomanni.pt@gmail.com";
    // StartTls (default), SslOnConnect, Auto, None (SMTP locale di sviluppo).
    public string Security { get; set; } = "StartTls";
    public string ContactRecipient { get; set; } = "alessandrosaccomanni.pt@gmail.com";
}

public class MailService(IOptions<MailOptions> options, ILogger<MailService> logger)
{
    private readonly MailOptions _options = options.Value;

    private bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Host);

    private SecureSocketOptions SocketOptions => _options.Security switch
    {
        "None" => SecureSocketOptions.None,
        "SslOnConnect" => SecureSocketOptions.SslOnConnect,
        "Auto" => SecureSocketOptions.Auto,
        _ => SecureSocketOptions.StartTls
    };

    public async Task<bool> InviaEmailAsync(string destinatario, string oggetto, string contenuto, string? replyTo = null)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("SMTP non configurato: email a {Destinatario} non inviata", destinatario);
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_options.From));
            message.To.Add(MailboxAddress.Parse(destinatario));
            message.Subject = oggetto;
            if (!string.IsNullOrWhiteSpace(replyTo))
            {
                message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
            }

            message.Body = new TextPart("plain") { Text = contenuto };

            using var client = new SmtpClient();
            await client.ConnectAsync(_options.Host, _options.Port, SocketOptions);
            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password);
            }
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            logger.LogInformation("Mail inviata correttamente a: {Destinatario}", destinatario);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Errore nell'invio della mail a {Destinatario}", destinatario);
            return false;
        }
    }

    public Task<bool> InviaFormAsync(FormInformazioni form)
    {
        var contenuto =
            "Nome: " + form.Nome + "\n" +
            "Cognome: " + form.Cognome + "\n" +
            "Messaggio: " + form.Messaggio + "\n";

        return InviaEmailAsync(_options.ContactRecipient, "Richiesta informazioni", contenuto, form.Email);
    }
}
