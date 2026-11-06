using AsCoachPT.Application.Models;
using AsCoachPT.Application.Services;
using AsCoachPT.Infrastructure;
using AsCoachPT.Infrastructure.Data;
using AsCoachPT.UI.Components;
using AsCoachPT.UI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

// Configura Blazor, autenticazione a cookie, MudBlazor e i servizi dell'applicazione.
var creatoreApp = WebApplication.CreateBuilder(args);

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

creatoreApp.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opzioni =>
    {
        opzioni.LoginPath = "/login";
        opzioni.AccessDeniedPath = "/";
        opzioni.Cookie.Name = "ascoach.auth";
        opzioni.Cookie.HttpOnly = true;
        opzioni.Cookie.SameSite = SameSiteMode.Lax;
        opzioni.SlidingExpiration = true;
        opzioni.ExpireTimeSpan = TimeSpan.FromDays(7);
    });
creatoreApp.Services.AddAuthorization();

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

    var username = form["username"].ToString().Trim();
    var password = form["password"].ToString();

    var utente = await utenti.VerificaCredenzialiAsync(username, password);
    if (utente is null)
    {
        return Results.Redirect("/login?error=1");
    }

    await http.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        ClaimsPrincipalFactory.Crea(utente),
        new AuthenticationProperties { IsPersistent = true });

    return Results.Redirect("/");
}).WithMetadata(new RequireAntiforgeryTokenAttribute(false));

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
        Username = form["username"].ToString().Trim(),
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
    return Results.Redirect("/");
}).WithMetadata(new RequireAntiforgeryTokenAttribute(false));

// Crea lo schema se assente e inserisce ruoli e servizi iniziali.
using (var ambito = applicazioneWeb.Services.CreateScope())
{
    var contesto = ambito.ServiceProvider.GetRequiredService<AsCoachDbContext>();
    await contesto.Database.EnsureCreatedAsync();
    DbSeeder.Seed(contesto);
}

applicazioneWeb.Run();

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
