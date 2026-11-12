# AsCoachPT — Alessandro Saccomanni Personal Trainer

Sito di gestione appuntamenti e contenuti del personal trainer Alessandro Saccomanni.
Applicazione **Blazor Web App (.NET 10)** con **MudBlazor**, database **PostgreSQL** in
produzione (SQLite in locale come fallback), deploy su **Render** tramite Docker.

La repository React/Spring originale (`FRONT_END-CAPSTONE` + `CAPSTONE`) resta invariata.

---

## 1. Funzionalità

### Area pubblica
- Home con hero a tutta pagina, servizi, “Chi sono”, contatti e footer.
- Sezione servizi editoriale a righe numerate, con immagine, descrizione e dettagli in dialogo.
- Pagina indirizzi con schede in stile “mappa + dettagli”, mappa Google caricata su richiesta.
- Prenotazione online con calendario, orari disponibili e selezione dello studio.
- Registrazione con nome, cognome, data di nascita, email, telefono e password.
- Login con **email** (lo username storico resta come fallback temporaneo).
- Recupero password tramite link email monouso.
- Login con Google per account **già registrati** e con **email Google verificata**.

### Area autenticata
- Prenotazioni future dell’utente.
- Cambio password dalla navbar.
- Logout manuale e logout automatico.

### Area staff (`ADMIN`, `PERSONAL_TRAINER`)
- Elenco utenti con telefono, ruolo, stato e azioni.
- Creazione/modifica utenti.
- Reimpostazione password tramite link monouso.
- Gestione indirizzi/studi: creazione, modifica, abilitazione/disabilitazione, eliminazione.
- Gestione prenotazioni: modifica ed eliminazione.
- Notifiche in-app quando un utente modifica o annulla una prenotazione.
- Il Personal Trainer può reimpostare la password solo degli utenti `USER`; l’Admin può
  reimpostarla a tutti.

---

## 2. Struttura della soluzione

```
AsCoachPT.sln
AsCoachPT.Domain/          Entità: Utente, Indirizzo, Servizio, Prenotazione, NotificaPrenotazione
AsCoachPT.Application/     Modelli, interfacce repository, servizi applicativi
AsCoachPT.Infrastructure/  EF Core, repository, email SMTP/Resend, BCrypt, DI
AsCoachPT.UI/              Blazor, MudBlazor, pagine, cookie auth, asset, JS/CSS
Dockerfile                 immagine di produzione
render.yaml                blueprint Render: database PostgreSQL + web service
.github/workflows/         deploy automatico su Render da main
```

Direzione delle dipendenze: `UI → Application → Domain`; `Infrastructure` implementa le
interfacce di Application ed è registrata con `AddInfrastructure(configuration)`.

### Servizi principali

| Servizio | Responsabilità |
|---|---|
| `UtenteService` | Registrazione, credenziali, profilo, ruoli |
| `PasswordResetService` | Token monouso, reset, cambio password |
| `PrenotazioneService` | Creazione/modifica/annullamento, disponibilità, email e notifiche staff |
| `IndirizzoService` | Studi, giorni di ricevimento, attivazione/disattivazione |
| `ServizioService` | Catalogo servizi |
| `ContattiService` | Modulo contatti |
| `SmtpEmailSender` | Invio SMTP (Gmail/MailKit) |
| `ResendEmailSender` | Invio HTTP tramite Resend |
| `BcryptPasswordHasher` | Hash e verifica password |
| `ClaimsPrincipalFactory` | Claims di identità, ruolo e versione sicurezza |

### Pagine principali

| Percorso | Descrizione |
|---|---|
| `/` | Home |
| `/servizi` | Servizi (pagina dedicata) |
| `/chiSono` | “Chi sono” (pagina dedicata) |
| `/indirizzo` | Indirizzi e mappe pubbliche |
| `/creaprenotazione` | Prenotazione |
| `/prenotazioni` | Prenotazioni utente/staff |
| `/gestioneIndirizzi` | Gestione studi (staff) |
| `/utenti` | Gestione utenti (staff) |
| `/login` | Login email/password e Google |
| `/registrazione` | Registrazione |
| `/password-dimenticata` | Richiesta link di reset |
| `/reimposta-password` | Impostazione nuova password tramite token |
| `/cambia-password` | Cambio password da autenticato |

---

## 3. Autenticazione e sessioni

### Login con email
- L’identificativo principale è l’**email**.
- Per compatibilità, il login accetta ancora il vecchio **username** se l’email non viene
  trovata. Questo fallback può essere rimosso quando tutti gli utenti sono migrati.
- La ricerca email è case-insensitive (`lower(email)`).
- Per i nuovi utenti l’username interno nel database viene impostato automaticamente
  all’email; la colonna resta per compatibilità e non viene mostrata nell’interfaccia.

### Cookie e durata
- Cookie `ascoach.auth`, `HttpOnly`, `SameSite=Lax`, non persistente.
- Sessione **assoluta di 30 minuti dal login**: non viene rinnovata dall’attività.
- Il client usa il cookie leggibile `ascoach.session_start` per il countdown e il logout
  automatico, poi redirige a `/login?timeout=1`.
- Il cookie di sessione sparisce alla chiusura del browser.
- Ogni avvio dell’applicazione genera un identificativo (`ClaimAvvio`): i cookie emessi
  prima di un riavvio/deploy vengono rifiutati e richiedono un nuovo login.
- Il claim `versione` viene confrontato con `VersioneSicurezza` dell’utente: cambi e reset
  password invalidano le sessioni precedenti.

### Ruoli
- `ADMIN`: accesso completo.
- `PERSONAL_TRAINER`: gestione utenti (solo utenti `USER` per i reset password),
  indirizzi e prenotazioni.
- `USER`: proprie prenotazioni e proprio profilo.

### Login Google
- Provider: `Microsoft.AspNetCore.Authentication.Google`.
- Endpoint: `/auth/google` → Google → `/signin-google` → `/auth/google/callback`.
- Accetta solo:
  - email Google **verificata** (`email_verified`);
  - email già presente in `utenti`.
- **Nessuna registrazione automatica**: un account Google non registrato viene rifiutato.
- Usa lo stesso cookie, la stessa scadenza e gli stessi claim del login tradizionale.
- Il provider viene registrato solo se `ClientId` e `ClientSecret` sono configurati; in caso
  contrario il pulsante non viene mostrato.

Configurazione Google Cloud:
- Tipo client: **Web application**.
- Redirect URI:
  - `https://ascoach.it/signin-google`
  - `http://localhost:5062/signin-google`
- Se l’app è in “Testing”, aggiungere l’account come test user o pubblicarla.

---

## 4. Password

### Password dimenticata
1. L’utente inserisce l’email in `/password-dimenticata`.
2. Se l’utente esiste, viene generato un token casuale da 32 byte.
3. Nel database viene salvato solo l’hash SHA-256 (`reset_token_hash`) e la scadenza
   (`reset_token_scadenza`, 30 minuti).
4. Il link viene inviato via email.
5. `/reimposta-password?token=...` valida il token, imposta la nuova password, cancella il
   token e incrementa `VersioneSicurezza`.
6. Il token è monouso e un nuovo token invalida il precedente.
7. La pagina risponde sempre in modo generico, per non rivelare se l’email esiste.

### Cambio password autenticato
- `/cambia-password`: password attuale + nuova password + conferma.
- Dopo il cambio la sessione viene invalidata e si viene rimandati al login.

### Reset da staff
- Menu azioni utente → “Reimposta password”.
- Genera un link valido 30 minuti e tenta l’invio email.
- Se l’email non parte, il link viene mostrato nel dialog con pulsante “Copia link”.
- Il Personal Trainer può farlo solo per utenti `USER`.

Requisiti password: minimo 8 caratteri per reset e cambio password.

---

## 5. Prenotazioni

- Calendario con giorni di ricevimento configurati per ogni studio.
- Slot a passi di 15 minuti, con durata del servizio e controlli su apertura/chiusura.
- Sabato con chiusura anticipata.
- Esclusione di domenica, date passate e sovrapposizioni.
- Gli studi disabilitati non compaiono e liberano i giorni di ricevimento.
- L’utente vede solo le proprie prenotazioni; lo staff vede tutte.

### Email automatiche
| Evento | Destinatario | Effetto |
|---|---|---|
| Registrazione | Utente | Email di benvenuto |
| Prenotazione creata | Utente | Conferma con dettagli |
| Prenotazione modificata dall’utente | Utente | Aggiornamento |
| Prenotazione annullata dall’utente | Utente | Cancellazione |
| Prenotazione modificata/annullata dall’utente | AsCoach | Avviso allo staff |
| Prenotazione modificata/annullata da Admin/PT | Utente | Solo email all’utente |

Gli avvisi staff vengono inviati a `Mail:StaffNotificationRecipient`
(`alessandrosaccomanni.pt@gmail.com`), non alle email personali dei singoli membri.

### Notifiche in-app
- Tabella `notifiche_prenotazioni`.
- Quando un utente modifica o annulla una prenotazione, viene salvato un messaggio.
- In `/prenotazioni` lo staff vede un pannello evidenziato con le notifiche non lette e il
  pulsante “Segna come lette”.
- Le notifiche non vengono create quando l’azione è fatta da Admin/PT.

---

## 6. Indirizzi e mappe

- Ricerca indirizzo con suggerimenti Google durante la digitazione.
- Selezione esplicita del suggerimento, senza scelta automatica del primo risultato.
- Selezione diretta dalla mappa con marker e reverse geocoding.
- Reverse geocoding che preferisce l’indirizzo stradale più preciso (via + civico).
- Coordinate formattate con cultura invariante: con `it-IT` la virgola decimale romperebbe
  l’URL di Google Maps.
- Pagina pubblica con schede “mappa + dettagli” in stile Maddalena: griglia a 2 colonne,
  1 colonna su mobile, mappa caricata su richiesta.
- Abilitazione/disabilitazione studio con liberazione dei giorni di ricevimento.

---

## 7. Interfaccia

### Palette e tipografia
- Colore principale: **OliveDrab `#6B8E23`** (W3Schools).
- Toni derivati: `#57731D` (hover), `#8FA94A` (chiaro), superfici `#E3E9D2` / `#F2F4E9`.
- Font: **Fraunces** (display) + **Manrope** (testo).
- Tema MudBlazor allineato alla palette.

### Comportamenti UI
- Hero sempre visibile interamente anche su schermi bassi; freccia e pulsanti non tagliati.
- Nome utente in navbar come chip cliccabile per il cambio password.
- Footer con soli link Home, Indirizzi, Prenota.
- Tabelle con colonna Azioni per prima e menu a tre puntini.
- Dialog di creazione/modifica larghi ma con campi proporzionati (max-width dedicata).
- Servizi a righe editoriali, non card.
- Animazioni di comparsa con `IntersectionObserver` + `MutationObserver`, per non perdere
  gli elementi dopo le navigazioni Blazor.

---

## 8. Database

Tabelle principali: `utenti`, `ruoli`, `indirizzi`, `servizi`, `prenotazioni`,
`notifiche_prenotazioni`.

### Adeguamenti automatici
`DatabaseSchema.AssicuraAsync` viene eseguito all’avvio dopo `EnsureCreated` e aggiunge in
modo idempotente le colonne/tabelle mancanti ai database esistenti:

- `utenti.telefono`
- `utenti.reset_token_hash`
- `utenti.reset_token_scadenza`
- `utenti.versione_sicurezza`
- `indirizzi.attivo`
- tabella `notifiche_prenotazioni`

Sono gestiti sia PostgreSQL (Npgsql) sia SQLite.

Seed automatici: ruoli, massaggi e consulenze iniziali se assenti.

---

## 9. Configurazione

### Connessione database
- Sviluppo: User Secrets `ConnectionStrings:Default`.
- Produzione: variabile `DATABASE_URL` fornita da Render.
- Fallback locale: SQLite `ascoachpt.db`.

```bash
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5432;Database=ascoachpt;Username=postgres;Password=***" \
  --project AsCoachPT.UI
```

### Email

Variabili/chiavi supportate:

| Chiave / variabile | Uso |
|---|---|
| `Mail:Host` / `Mail__Host` | Host SMTP (default `smtp.gmail.com`) |
| `Mail:Port` / `Mail__Port` | Porta SMTP |
| `Mail:Security` / `Mail__Security` | `StartTls`, `SslOnConnect`, `Auto`, `None` |
| `Mail:Username` / `MAIL_USERNAME` | Account SMTP |
| `Mail:Password` / `MAIL_PASSWORD` | App Password Gmail |
| `Mail:From` / `MAIL_FROM` | Mittente |
| `Mail:StaffNotificationRecipient` | Destinatario avvisi staff |
| `Resend:ApiKey` / `RESEND_API_KEY` | API key Resend |
| `Resend:From` / `RESEND_FROM` | Mittente Resend |
| `Resend:ReplyTo` / `RESEND_REPLY_TO` | Reply-To Resend |
| `Email:Provider` / `EMAIL_PROVIDER` | `Smtp`, `Resend` o automatico |

Regole di scelta del provider:
- `EMAIL_PROVIDER=Resend` → Resend (se la API key è presente).
- `EMAIL_PROVIDER=Smtp` → SMTP.
- Automatico → SMTP se username e password sono configurati; altrimenti Resend.

### Google
| Chiave / variabile | Uso |
|---|---|
| `Authentication:Google:ClientId` / `GOOGLE_CLIENT_ID` | Client ID OAuth |
| `Authentication:Google:ClientSecret` / `GOOGLE_CLIENT_SECRET` | Client Secret OAuth |

### Segreti locali
```bash
dotnet user-secrets set "Mail:Username" "alessandrosaccomanni.pt@gmail.com" --project AsCoachPT.UI
dotnet user-secrets set "Mail:Password" "<App Password Gmail>" --project AsCoachPT.UI
dotnet user-secrets set "Mail:From" "alessandrosaccomanni.pt@gmail.com" --project AsCoachPT.UI
dotnet user-secrets set "Authentication:Google:ClientId" "<client-id>" --project AsCoachPT.UI
dotnet user-secrets set "Authentication:Google:ClientSecret" "<client-secret>" --project AsCoachPT.UI
```

Non committare mai `Mail:Password`, `Resend:ApiKey` o Client Secret.

---

## 10. Avvio locale

Requisiti: .NET SDK 10, PostgreSQL locale (oppure SQLite), eventuale App Password Gmail.

Visual Studio:
1. Aprire `AsCoachPT.sln`.
2. Impostare **AsCoachPT.UI** come progetto di avvio.
3. Avviare con il profilo `http`.
4. Aprire `http://localhost:5062`.

Da CLI:

```bash
dotnet run --project AsCoachPT.UI
```

Il profilo di lancio attuale espone `http://localhost:5062`.

---

## 11. Pubblicazione

| Ramo | Uso | Deploy |
|---|---|---|
| `develop` | sviluppo e test | nessun deploy automatico |
| `main` | produzione | deploy automatico su Render |

Flusso:
1. commit su `develop`;
2. verifica locale;
3. merge `develop` → `main`;
4. push su `main`;
5. GitHub Actions `.github/workflows/deploy-render.yml` avvia il deploy Render del commit e
   ne segue lo stato fino a `live`;
6. Render ricostruisce l’immagine Docker e aggiorna `https://ascoach.it`.

Variabili Render principali:
- `ASPNETCORE_ENVIRONMENT=Production`
- `DATABASE_URL`
- `MAIL_USERNAME`, `MAIL_PASSWORD`, `MAIL_FROM`
- `RESEND_API_KEY`, `RESEND_FROM`, `RESEND_REPLY_TO`
- `EMAIL_PROVIDER=Resend`
- `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`

`render.yaml` dichiara le variabili sensibili con `sync: false`: i valori vanno impostati
nel dashboard Render, non nel repository.

### Nota importante sulle email in produzione
Render **blocca le connessioni SMTP in uscita** (testate 587 e 465: timeout). Gmail SMTP
funziona in locale ma non dal servizio Render.

Configurazione attualmente funzionante in produzione:
- `EMAIL_PROVIDER=Resend`
- mittente: `Alessandro Saccomanni <noreply@ascoach.it>`
- `Reply-To: alessandrosaccomanni.pt@gmail.com`

Per un mittente esattamente `alessandrosaccomanni.pt@gmail.com` serve un provider HTTP che
consenta la verifica del singolo mittente Gmail (Brevo/SendGrid) oppure un relay Google
Apps Script/Gmail API. Resend non accetta `gmail.com` come dominio mittente.

---

## 12. Sicurezza

- Password BCrypt.
- Token reset salvato solo come hash SHA-256, scadenza 30 minuti, monouso.
- Risposte generiche nel recupero password per evitare enumerazione utenti.
- Ruoli e autorizzazioni su pagine ed endpoint.
- Antiforgery sugli endpoint POST; controllo same-origin aggiuntivo.
- Cookie `HttpOnly`, `SameSite=Lax`, sessione assoluta.
- Invalidazione sessioni al riavvio e al cambio/reset password.
- Google login solo con email verificata e account esistente.
- Header di sicurezza; CSP attualmente in `Report-Only` per non bloccare Google Maps/font.

---

## 13. Verifiche e stato

Build:

```bash
dotnet build AsCoachPT.sln
```

Non esiste ancora un progetto di test automatici; le verifiche sono state eseguite con build
e smoke test manuali su:
- registrazione e login email;
- login legacy con username;
- reset password e token monouso;
- email di benvenuto, conferma, modifica e annullamento;
- notifiche staff in-app;
- Google OAuth (client e redirect verificati);
- migrazioni database e deploy Render.

Stato produzione attuale:
- autenticazione email: funzionante;
- Google OAuth: redirect e client validi; login reale da verificare con account Google;
- email: funzionanti via Resend da `noreply@ascoach.it` con Reply-To Gmail;
- invio diretto da Gmail in produzione: bloccato dal limite SMTP di Render.
