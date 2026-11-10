using AsCoachPT.Application.Models;
using AsCoachPT.Application.Repositories;
using AsCoachPT.Application.Services;
using AsCoachPT.Infrastructure.Data;
using AsCoachPT.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AsCoachPT.Infrastructure;

/// <summary>Registra database, repository e servizi di infrastruttura.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Configura EF Core (PostgreSQL o SQLite), i repository, la posta SMTP e la codifica password.
    /// </summary>
    /// <remarks>
    /// La stringa di connessione arriva da DATABASE_URL/SPRING_DATASOURCE_URL (Render) oppure da
    /// ConnectionStrings:Default (User Secrets in sviluppo). Se manca, si usa un file SQLite locale.
    /// Le credenziali SMTP possono arrivare da Mail:Username/Mail:Password o dalle variabili
    /// MAIL_USERNAME/MAIL_PASSWORD già usate dal backend precedente.
    /// </remarks>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection servizi,
        IConfiguration configurazione)
    {
        var stringaConnessione =
            Environment.GetEnvironmentVariable("DATABASE_URL") ??
            Environment.GetEnvironmentVariable("SPRING_DATASOURCE_URL") ??
            configurazione.GetConnectionString("Default") ??
            "Data Source=ascoachpt.db";

        servizi.AddDbContext<AsCoachDbContext>(opzioni =>
        {
            if (stringaConnessione.StartsWith("postgres", StringComparison.OrdinalIgnoreCase) ||
                stringaConnessione.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            {
                opzioni.UseNpgsql(NormalizzaPostgres(stringaConnessione));
            }
            else
            {
                opzioni.UseSqlite(stringaConnessione);
            }
        });

        servizi.AddOptions<MailOptions>()
            .Configure(opzioni => configurazione.GetSection(MailOptions.SectionName).Bind(opzioni))
            .PostConfigure(opzioni =>
            {
                opzioni.Username = string.IsNullOrWhiteSpace(opzioni.Username)
                    ? Environment.GetEnvironmentVariable("MAIL_USERNAME") ?? string.Empty
                    : opzioni.Username;
                opzioni.Password = string.IsNullOrWhiteSpace(opzioni.Password)
                    ? Environment.GetEnvironmentVariable("MAIL_PASSWORD") ?? string.Empty
                    : opzioni.Password;
                var mittenteDaAmbiente = Environment.GetEnvironmentVariable("MAIL_FROM");
                if (!string.IsNullOrWhiteSpace(mittenteDaAmbiente))
                {
                    opzioni.From = mittenteDaAmbiente;
                }
            });

        servizi.AddScoped<IUtenteRepository, UtenteRepository>();
        servizi.AddScoped<IIndirizzoRepository, IndirizzoRepository>();
        servizi.AddScoped<IServizioRepository, ServizioRepository>();
        servizi.AddScoped<IPrenotazioneRepository, PrenotazioneRepository>();
        servizi.AddScoped<IUnitOfWork, UnitOfWork>();

        servizi.AddOptions<ResendOptions>()
            .Configure(opzioni => configurazione.GetSection(ResendOptions.SectionName).Bind(opzioni))
            .PostConfigure(opzioni =>
            {
                opzioni.ApiKey = string.IsNullOrWhiteSpace(opzioni.ApiKey)
                    ? Environment.GetEnvironmentVariable("RESEND_API_KEY") ?? string.Empty
                    : opzioni.ApiKey;
                opzioni.From = string.IsNullOrWhiteSpace(opzioni.From)
                    ? Environment.GetEnvironmentVariable("RESEND_FROM") ?? "onboarding@resend.dev"
                    : opzioni.From;
            });

        var smtpUsername = configurazione["Mail:Username"];
        if (string.IsNullOrWhiteSpace(smtpUsername))
        {
            smtpUsername = Environment.GetEnvironmentVariable("MAIL_USERNAME");
        }

        var smtpPassword = configurazione["Mail:Password"];
        if (string.IsNullOrWhiteSpace(smtpPassword))
        {
            smtpPassword = Environment.GetEnvironmentVariable("MAIL_PASSWORD");
        }

        var resendApiKey = configurazione["Resend:ApiKey"] ?? Environment.GetEnvironmentVariable("RESEND_API_KEY");
        var usaSmtp = !string.IsNullOrWhiteSpace(smtpUsername) && !string.IsNullOrWhiteSpace(smtpPassword);

        if (usaSmtp)
        {
            servizi.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else if (!string.IsNullOrWhiteSpace(resendApiKey))
        {
            servizi.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
                client.BaseAddress = new Uri("https://api.resend.com/"));
        }
        else
        {
            servizi.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        servizi.AddScoped<IPasswordHasher, BcryptPasswordHasher>();

        servizi.AddScoped<UtenteService>();
        servizi.AddScoped<PasswordResetService>();
        var emailStaff = configurazione["Mail:StaffNotificationRecipient"];
        if (string.IsNullOrWhiteSpace(emailStaff))
        {
            emailStaff = "alessandrosaccomanni.pt@gmail.com";
        }
        servizi.AddSingleton(new StaffNotificationSettings(emailStaff));
        servizi.AddScoped<IndirizzoService>();
        servizi.AddScoped<ServizioService>();
        servizi.AddScoped<PrenotazioneService>();
        servizi.AddScoped<ContattiService>();

        return servizi;
    }

    /// <summary>Converte una URL PostgreSQL (postgres://…) nel formato accettato da Npgsql.</summary>
    private static string NormalizzaPostgres(string stringaConnessione)
    {
        if (!stringaConnessione.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !stringaConnessione.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return stringaConnessione;
        }

        var uri = new Uri(stringaConnessione);
        var credenziali = uri.UserInfo.Split(':', 2);

        var sslMode = "Prefer";
        foreach (var coppia in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parti = coppia.Split('=', 2);
            if (parti.Length == 2 && parti[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
            {
                sslMode = parti[1] switch
                {
                    "disable" => "Disable",
                    "allow" => "Allow",
                    "prefer" => "Prefer",
                    "require" => "Require",
                    "verify-ca" => "VerifyCA",
                    "verify-full" => "VerifyFull",
                    _ => "Prefer"
                };
            }
        }

        return $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Database={uri.AbsolutePath.TrimStart('/')};Username={credenziali[0]};Password={Uri.UnescapeDataString(credenziali.ElementAtOrDefault(1) ?? string.Empty)};Ssl Mode={sslMode}";
    }
}
