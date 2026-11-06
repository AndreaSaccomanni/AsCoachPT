# AsCoach — Alessandro Saccomanni PT (.NET 10 + Blazor + MudBlazor)

Porting del sito di gestione appuntamenti del personal trainer dal progetto React + Spring Boot
(`AndreaSaccomanni/FRONT_END-CAPSTONE` + `AndreaSaccomanni/CAPSTONE`) a **ASP.NET Core .NET 10 con
Blazor (Interactive Server) e componenti MudBlazor**.

Il progetto React/Java resta invariato e non viene toccato da questa repository.

## Stack

| Area | Tecnologia |
|---|---|
| UI | Blazor Web App (.NET 10), interattività globale Server |
| Componenti | MudBlazor 9 (tabelle, dialog, form, picker, snackbar) + CSS originale del sito |
| Persistenza | EF Core 10 + Npgsql (PostgreSQL) / SQLite per sviluppo rapido |
| Auth | Cookie di autenticazione + claim di ruolo (`USER`, `ADMIN`, `PERSONAL_TRAINER`), hash BCrypt compatibili con Spring |
| Email | MailKit (SMTP Gmail), config opzionale |

## Struttura

```
AsCoach.sln
src/AsCoachPT/
  Components/Pages/      # Home, ChiSono, Servizi, Contatti, Login, Registrazione,
                         # Prenota, Prenotazioni, Utenti, Indirizzi, GestioneIndirizzi
  Components/Layout/     # Navbar, Footer, MainLayout (provider MudBlazor + tema)
  Domain/                # Entità e enum
  Data/                  # DbContext (mapping schema Hibernate), seeder
  Services/              # Prenotazioni (regole slot), Utenti, Indirizzi, Mail, Password
  wwwroot/               # CSS originale, immagini, video, JS (cursore, animazioni, Google Places)
  Dockerfile             # immagine di produzione per Render
```

## Avvio in Visual Studio

1. Aprire `AsCoach.sln`.
2. Impostare `AsCoachPT` come progetto di avvio e premere F5.

In sviluppo la connessione predefinita è SQLite (`ascoach.db`). Per usare il clone PostgreSQL
locale (`ascoach_dotnet`):

```bash
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5432;Database=ascoach_dotnet;Username=postgres;Password=***" \
  --project src/AsCoachPT
```

Da CLI:

```bash
dotnet run --project src/AsCoachPT
```

## Database

Il database di riferimento del progetto React è stato clonato in **`ascoach_dotnet`** sullo stesso
PostgreSQL 17 locale (utente `postgres`) e registrato in DBeaver come
`AsCoach .NET (ascoach_dotnet)`.

Lo schema è identico a quello generato da Hibernate: `utenti`, `servizi` (TPH con
`servizio_type`), `prenotazioni`, `indirizzi`, `ruoli`. Le tabelle legacy `tipo_servizi` /
`dettaglio_servizi` presenti nel DB di origine non sono usate.

## Configurazione

| Impostazione | Sviluppo | Produzione |
|---|---|---|
| Database | user secrets / `ConnectionStrings:Default` | `DATABASE_URL` o `SPRING_DATASOURCE_URL` |
| SMTP | `Mail:Username` / `Mail:Password` (o secrets) | `MAIL_USERNAME` / `MAIL_PASSWORD` |
| Porta | `launchSettings.json` | `PORT` |

Senza credenziali SMTP l'invio email fallisce in modo esplicito (log + messaggio in UI), senza
bloccare prenotazioni e registrazioni.

## Deploy (Render) e dominio

1. Nuovo **Web Service** Docker dal repository, root directory `src/AsCoachPT`.
2. Env: `DATABASE_URL`, `MAIL_USERNAME`, `MAIL_PASSWORD`.
3. Puntare `ascoach.it` / `www.ascoach.it` a questo servizio invece del sito statico React.

Blazor Server richiede una connessione persistente (SignalR), supportata dal web service Docker.
Con il routing lato server non serve più il rewrite SPA che oggi causa i 404 sui deep link.

## Note funzionali

- Regole prenotazioni identiche all'originale: apertura 09:00, chiusura 20:30, sabato entro le
  13:00, domenica chiusa, slot da 15 minuti, controllo sovrapposizioni.
- Giorni settimanali assegnabili a un solo studio; eliminazione indirizzo con prenotazioni
  collegate richiede conferma.
- Google Places per l'autocomplete indirizzi: richiede una API key con billing attivo (la key
  attuale del sito restituisce `BillingNotEnabledMapError`).
