# AsCoachPT — Alessandro Saccomanni Personal Trainer

Sito di gestione appuntamenti del personal trainer: Blazor Web App (.NET 10) con MudBlazor,
organizzato sui quattro progetti come il sito Maddalena Pimponi.

La repository React/Spring originale (`FRONT_END-CAPSTONE` + `CAPSTONE`) resta invariata.

## Struttura

```
AsCoachPT.sln
AsCoachPT.Domain/          Entità e regole di dominio (utenti, studi, servizi, prenotazioni)
AsCoachPT.Application/     Modelli applicativi, interfacce repository, servizi di business
AsCoachPT.Infrastructure/  EF Core, repository, SMTP, BCrypt, registrazione DI
AsCoachPT.UI/              Blazor, MudBlazor, pagine, autenticazione a cookie, asset
Dockerfile                 immagine di produzione (Render)
render.yaml                blueprint Render: database + web service
```

Direzione delle dipendenze: `UI → Application → Domain`; `Infrastructure` implementa le
interfacce di Application e viene registrata con `AddInfrastructure(configuration)`.

## Avvio in Visual Studio

1. Aprire `AsCoachPT.sln`.
2. Impostare **AsCoachPT.UI** come progetto di avvio e il profilo **https**.
3. F5 → `https://localhost:7039`.

Da CLI:

```bash
dotnet run --project AsCoachPT.UI
```

In sviluppo la connessione arriva dai **User Secrets** del progetto UI
(`ConnectionStrings:Default`), altrimenti si usa un file SQLite `ascoachpt.db`.

Esempio per collegare il clone PostgreSQL locale:

```bash
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5432;Database=ascoachpt;Username=postgres;Password=***" \
  --project AsCoachPT.UI
```

## Database

Schema identico a quello generato da Hibernate (`utenti`, `servizi` con `servizio_type`,
`prenotazioni`, `indirizzi`, `ruoli`). In produzione il database è il PostgreSQL Render
`ascoachpt-db`; in locale il clone `ascoachpt` è registrato in DBeaver.
I dati iniziali (ruoli, massaggi, consulenze) vengono inseriti automaticamente se mancanti.

## Email

L'invio usa MailKit con configurazione `Mail` (o `MAIL_USERNAME`/`MAIL_PASSWORD`).
Il piano **free** di Render blocca le porte SMTP in uscita: per le email serve un piano
superiore oppure un provider HTTP. Senza configurazione l'invio restituisce errore in UI
senza bloccare prenotazioni e registrazioni.

## Pubblicazione

| Ramo | Uso | Deploy |
|---|---|---|
| `develop` | sviluppo e test | nessun deploy automatico |
| `main` | produzione | Render builda il Dockerfile e aggiorna `ascoach.it` |

Flusso: commit su `develop` → push → verifica locale → merge/pull request verso `main` →
Render avvia automaticamente build e deploy → `https://ascoach.it` aggiornato.
Il web service free va in sleep dopo inattività: il primo accesso può richiedere qualche secondo.

Prima del primo deploy della nuova struttura il servizio Render deve puntare alla root della
repository (`rootDir` vuoto) e al `Dockerfile` in radice.
