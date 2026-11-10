using System.Security.Claims;
using AsCoachPT.Application.Models;
using AsCoachPT.Application.Services;
using AsCoachPT.Domain.Models;
using AsCoachPT.Infrastructure;
using AsCoachPT.Infrastructure.Data;
using AsCoachPT.UI.Components;
using AsCoachPT.UI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

// Configura Blazor, autenticazione a cookie, MudBlazor e i servizi dell'applicazione.
var creatoreApp = WebApplication.CreateBuilder(args);

// Ogni avvio invalida le sessioni precedenti: fermare il debug o riavviare
// il sito obbliga a effettuare di nuovo il login.
var avvioSessione = Guid.NewGuid().ToString("N");
const string ClaimAvvio = "avvio";
const string CookieInizioSessione = "ascoach.session_start";
var durataSessione = TimeSpan.FromMinutes(30);

var googleClientId = creatoreApp.Configuration["Authentication:Google:ClientId"];
if (string.IsNullOrWhiteSpace(googleClientId))
{
    googleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
}

var googleClientSecret = creatoreApp.Configuration["Authentication:Google:ClientSecret"];
if (string.IsNullOrWhiteSpace(googleClientSecret))
{
    googleClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");
}

var googleAbilitato = !string.IsNullOrWhiteSpace(googleClientId)
    && !string.IsNullOrWhiteSpace(googleClientSecret);
creatoreApp.Services.AddSingleton(new GoogleLoginSettings(googleAbilitato));

// Render e altri host forniscono la porta tramite variabile d'ambiente.
var portaDaAmbiente = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(portaDaAmbiente))
{
    creatoreApp.WebHost.UseUrls($"http://0.0.0.0:{portaDaAmbiente}");
}

creatoreApp.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
creatoreApp.Services.AddCascadingAuthenticationState();
creatoreApp.Services.AddHttpContextAccessor();
creatoreApp.Services.AddAntiforgery();
creatoreApp.Services.AddMudServices();
creatoreApp.Services.AddInfrastructure(creatoreApp.Configuration);
creatoreApp.Services.AddResponseCompression(opzioni => opzioni.EnableForHttps = true);

var autenticazione = creatoreApp.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opzioni =>
    {
        opzioni.LoginPath = "/login";
        opzioni.AccessDeniedPath = "/";
        opzioni.Cookie.Name = "ascoach.auth";
        opzioni.Cookie.HttpOnly = true;
        opzioni.Cookie.SameSite = SameSiteMode.Lax;
        opzioni.Cookie.IsEssential = true;

        // Sessione assoluta: 30 minuti dal login, poi serve un nuovo accesso.
        opzioni.ExpireTimeSpan = durataSessione;
        opzioni.SlidingExpiration = false;

        opzioni.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async contesto =>
            {
                var avvio = contesto.Principal?.FindFirst(ClaimAvvio)?.Value;
                var utenteId = contesto.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var versione = contesto.Principal?.FindFirst(ClaimsPrincipalFactory.VersioneClaim)?.Value;

                var valido = string.Equals(avvio, avvioSessione, StringComparison.Ordinal);
                var idUtente = 0L;
                var versioneSessione = 0;

                if (valido)
                {
                    valido = long.TryParse(utenteId, out idUtente)
                        && int.TryParse(versione, out versioneSessione);
                }

                if (valido)
                {
                    var database = contesto.HttpContext.RequestServices.GetRequiredService<AsCoachDbContext>();
                    var versioneAttuale = await database.Utenti
                        .AsNoTracking()
                        .Where(utente => utente.Id == idUtente)
                        .Select(utente => (int?)utente.VersioneSicurezza)
                        .FirstOrDefaultAsync();

                    valido = versioneAttuale == versioneSessione;
                }

                if (!valido)
                {
                    contesto.RejectPrincipal();
                    await contesto.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    contesto.HttpContext.Response.Cookies.Delete(CookieInizioSessione, new CookieOptions { Path = "/" });
                }
            }
        };
    });

if (googleAbilitato)
{
    autenticazione
        .AddCookie("GoogleExternal", opzioni =>
        {
            opzioni.Cookie.Name = "ascoach.google.external";
            opzioni.Cookie.HttpOnly = true;
            opzioni.Cookie.SameSite = SameSiteMode.Lax;
            opzioni.Cookie.IsEssential = true;
            opzioni.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            opzioni.SlidingExpiration = false;
        })
        .AddGoogle(opzioni =>
        {
            opzioni.ClientId = googleClientId!;
            opzioni.ClientSecret = googleClientSecret!;
            opzioni.SignInScheme = "GoogleExternal";
            opzioni.CallbackPath = "/signin-google";
            opzioni.SaveTokens = false;
            opzioni.Scope.Add("email");
            opzioni.Scope.Add("profile");

            // L'email deve essere verificata da Google per poter accedere.
            opzioni.ClaimActions.MapJsonKey("email_verified", "email_verified");

            opzioni.Events.OnRemoteFailure = contesto =>
            {
                contesto.Response.Redirect("/login?googleError=failed");
                contesto.HandleResponse();
                return Task.CompletedTask;
            };
        });
}

creatoreApp.Services.AddAuthorization();

// Date e calendari in italiano (etichette dei picker MudBlazor).
var culturaItaliana = new System.Globalization.CultureInfo("it-IT");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culturaItaliana;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culturaItaliana;

var applicazioneWeb = creatoreApp.Build();

// Gestione errori e HTTPS solo fuori dallo sviluppo, come nel sito Maddalena.
if (!applicazioneWeb.Environment.IsDevelopment())
{
    applicazioneWeb.UseExceptionHandler("/Error", createScopeForErrors: true);
    applicazioneWeb.UseHsts();
}

// Intestazioni di sicurezza: la CSP è in sola segnalazione per non bloccare Google Maps e font.
applicazioneWeb.Use(async (contesto, successivo) =>
{
    var intestazioni = contesto.Response.Headers;
    intestazioni["X-Content-Type-Options"] = "nosniff";
    intestazioni["Referrer-Policy"] = "strict-origin-when-cross-origin";
    intestazioni["Permissions-Policy"] = "camera=(), geolocation=(), microphone=()";
    intestazioni["Content-Security-Policy-Report-Only"] =
        "default-src 'self'; script-src 'self' 'unsafe-inline' https://maps.googleapis.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data: https://maps.gstatic.com https://maps.googleapis.com; " +
        "frame-src https://www.google.com https://maps.google.com; connect-src 'self' ws: wss:; " +
        "form-action 'self'; base-uri 'self'; frame-ancestors 'self'";

    await successivo(contesto);
});

applicazioneWeb.UseResponseCompression();
applicazioneWeb.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
applicazioneWeb.UseAuthentication();
applicazioneWeb.UseAuthorization();
applicazioneWeb.UseAntiforgery();

applicazioneWeb.MapStaticAssets();
applicazioneWeb.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Endpoint di autenticazione: login, registrazione e logout via form HTML.
applicazioneWeb.MapPost("/auth/login", async (HttpContext http, UtenteService utenti, IFormCollection form) =>
{
    if (!StessaOrigine(http.Request))
    {
        return Results.BadRequest();
    }

    var identificativo = form["email"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(identificativo))
    {
        // Compatibilità con il vecchio form che inviava "username".
        identificativo = form["username"].ToString().Trim();
    }

    var password = form["password"].ToString();

    var utente = await utenti.VerificaCredenzialiAsync(identificativo, password);
    if (utente is null)
    {
        return Results.Redirect("/login?error=1");
    }

    await AccediUtenteAsync(http, utente);
    return Results.Redirect("/");
}).WithMetadata(new RequireAntiforgeryTokenAttribute(false));

applicazioneWeb.MapGet("/auth/google", (HttpContext http) =>
{
    if (!googleAbilitato)
    {
        return Results.Redirect("/login?googleError=config");
    }

    return Results.Challenge(
        new AuthenticationProperties { RedirectUri = "/auth/google/callback" },
        new[] { GoogleDefaults.AuthenticationScheme });
});

applicazioneWeb.MapGet("/auth/google/callback", async (HttpContext http, UtenteService utenti) =>
{
    var risultato = await http.AuthenticateAsync("GoogleExternal");
    if (!risultato.Succeeded || risultato.Principal is null)
    {
        return Results.Redirect("/login?googleError=failed");
    }

    var principalEsterno = risultato.Principal;
    var email = principalEsterno.FindFirstValue(ClaimTypes.Email)
        ?? principalEsterno.FindFirstValue("email");
    var verificata = principalEsterno.FindFirstValue("email_verified");

    await http.SignOutAsync("GoogleExternal");

    if (!bool.TryParse(verificata, out var emailVerificata) || !emailVerificata || string.IsNullOrWhiteSpace(email))
    {
        return Results.Redirect("/login?googleError=unverified");
    }

    var utente = await utenti.TrovaPerEmailAsync(email);
    if (utente is null)
    {
        // Nessuna registrazione automatica: Google accede solo ad account già esistenti.
        return Results.Redirect("/login?googleError=nonexistent");
    }

    await AccediUtenteAsync(http, utente);
    return Results.Redirect("/");
});

applicazioneWeb.MapPost("/auth/register", async (HttpContext http, UtenteService utenti, IFormCollection form) =>
{
    if (!StessaOrigine(http.Request))
    {
        return Results.BadRequest();
    }

    DateOnly? dataDiNascita = null;
    if (DateOnly.TryParse(form["dataDiNascita"], out var dataConvertita))
    {
        dataDiNascita = dataConvertita;
    }

    var password = form["password"].ToString();
    if (password != form["confirmPassword"].ToString())
    {
        return Results.Redirect("/registrazione?error=" + Uri.EscapeDataString("Le password non corrispondono."));
    }

    if (password.Length < 3)
    {
        return Results.Redirect("/registrazione?error=" + Uri.EscapeDataString("La password deve contenere almeno 3 caratteri."));
    }

    var richiesta = new RegisterRequest
    {
        Nome = form["nome"].ToString().Trim(),
        Cognome = form["cognome"].ToString().Trim(),
        DataDiNascita = dataDiNascita,
        Email = form["email"].ToString().Trim(),
        Telefono = form["telefono"].ToString().Trim(),
        Password = password,
        Ruolo = "USER"
    };

    try
    {
        await utenti.RegistraAsync(richiesta);
    }
    catch (InvalidOperationException eccezione)
    {
        return Results.Redirect("/registrazione?error=" + Uri.EscapeDataString(eccezione.Message));
    }

    return Results.Redirect("/login?registered=1");
}).WithMetadata(new RequireAntiforgeryTokenAttribute(false));

applicazioneWeb.MapPost("/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    http.Response.Cookies.Delete(CookieInizioSessione, new CookieOptions { Path = "/" });
    return Results.Redirect("/");
}).WithMetadata(new RequireAntiforgeryTokenAttribute(false));

// Crea lo schema se assente e inserisce ruoli e servizi iniziali.
using (var ambito = applicazioneWeb.Services.CreateScope())
{
    var contesto = ambito.ServiceProvider.GetRequiredService<AsCoachDbContext>();
    await contesto.Database.EnsureCreatedAsync();
    await DatabaseSchema.AssicuraAsync(contesto);
    DbSeeder.Seed(contesto);
}

applicazioneWeb.Run();

// Accede un utente già verificato (password o Google) con cookie e scadenza assoluta.
async Task AccediUtenteAsync(HttpContext http, Utente utente)
{
    var principal = ClaimsPrincipalFactory.Crea(utente);
    principal.Identities.First().AddClaim(new Claim(ClaimAvvio, avvioSessione));

    await http.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        new AuthenticationProperties { IsPersistent = false });

    http.Response.Cookies.Append(
        CookieInizioSessione,
        DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
        new CookieOptions
        {
            HttpOnly = false,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = http.Request.IsHttps,
            Path = "/"
        });
}

// Blocca richieste cross-origin verso gli endpoint di autenticazione (difesa CSRF aggiuntiva).
static bool StessaOrigine(HttpRequest richiesta)
{
    var origine = richiesta.Headers.Origin.ToString();
    if (string.IsNullOrEmpty(origine))
    {
        return true;
    }

    return Uri.TryCreate(origine, UriKind.Absolute, out var uri) && uri.Host == richiesta.Host.Host;
}
