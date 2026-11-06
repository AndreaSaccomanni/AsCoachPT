using AsCoach.Web.Components;
using AsCoach.Web.Data;
using AsCoach.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Render (e altri host) forniscono la porta via variabile PORT.
var portaDaAmbiente = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(portaDaAmbiente))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{portaDaAmbiente}");
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAntiforgery();
builder.Services.AddMudServices();

var connectionString =
    Environment.GetEnvironmentVariable("DATABASE_URL") ??
    Environment.GetEnvironmentVariable("SPRING_DATASOURCE_URL") ??
    builder.Configuration.GetConnectionString("Default") ??
    "Data Source=ascoach.db";
builder.Services.AddDbContext<AsCoachDbContext>(options =>
{
    if (connectionString.StartsWith("postgres", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(NormalizzaPostgres(connectionString));
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

builder.Services.AddOptions<MailOptions>()
    .Bind(builder.Configuration.GetSection("Mail"))
    .PostConfigure(options =>
    {
        // Compatibilità con le variabili d'ambiente del backend Spring esistente.
        options.Username = string.IsNullOrWhiteSpace(options.Username)
            ? Environment.GetEnvironmentVariable("MAIL_USERNAME") ?? string.Empty
            : options.Username;
        options.Password = string.IsNullOrWhiteSpace(options.Password)
            ? Environment.GetEnvironmentVariable("MAIL_PASSWORD") ?? string.Empty
            : options.Password;
    });
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<MailService>();
builder.Services.AddScoped<UtenteService>();
builder.Services.AddScoped<ServizioService>();
builder.Services.AddScoped<IndirizzoService>();
builder.Services.AddScoped<PrenotazioneService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/";
        options.Cookie.Name = "ascoach.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });
builder.Services.AddAuthorization();

var app = builder.Build();

static bool StessaOrigine(HttpRequest request)
{
    var origin = request.Headers.Origin.ToString();
    if (string.IsNullOrEmpty(origin))
    {
        return true;
    }

    return Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Host == request.Host.Host;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/auth/login", async (HttpContext http, UtenteService utenti, IFormCollection form) =>
{
    if (!StessaOrigine(http.Request))
    {
        return Results.BadRequest();
    }

    var username = form["username"].ToString().Trim();
    var password = form["password"].ToString();

    var utente = utenti.VerificaCredenziali(username, password);
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

app.MapPost("/auth/register", async (HttpContext http, UtenteService utenti, IFormCollection form) =>
{
    if (!StessaOrigine(http.Request))
    {
        return Results.BadRequest();
    }

    DateOnly? dataDiNascita = null;
    if (DateOnly.TryParse(form["dataDiNascita"], out var parsed))
    {
        dataDiNascita = parsed;
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

    var request = new RegisterRequest
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
        await utenti.RegistraAsync(request);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Redirect("/registrazione?error=" + Uri.EscapeDataString(ex.Message));
    }

    return Results.Redirect("/login?registered=1");
}).WithMetadata(new RequireAntiforgeryTokenAttribute(false));

app.MapPost("/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).WithMetadata(new RequireAntiforgeryTokenAttribute(false));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AsCoachDbContext>();
    db.Database.EnsureCreated();
    DbSeeder.Seed(db);
}

app.Run();

static string NormalizzaPostgres(string connectionString)
{
    // Render espone DATABASE_URL nel formato postgres://user:pass@host:port/db
    if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
        !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return connectionString;
    }

    var uri = new Uri(connectionString);
    var credenziali = uri.UserInfo.Split(':', 2);
    return $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Database={uri.AbsolutePath.TrimStart('/')};Username={credenziali[0]};Password={Uri.UnescapeDataString(credenziali.ElementAtOrDefault(1) ?? string.Empty)};Ssl Mode=Require";
}
