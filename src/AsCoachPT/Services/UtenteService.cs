using AsCoachPT.Data;
using AsCoachPT.Domain;
using Microsoft.EntityFrameworkCore;

namespace AsCoachPT.Services;

public class UtenteService(AsCoachDbContext db, PasswordService passwords, MailService mail)
{
    public async Task<Utente> RegistraAsync(RegisterRequest request)
    {
        if (await db.Utenti.AnyAsync(u => u.Email == request.Email))
        {
            throw new InvalidOperationException("Email già presente nel sistema");
        }

        if (await db.Utenti.AnyAsync(u => u.Username == request.Username))
        {
            throw new InvalidOperationException("Username già presente nel sistema");
        }

        var ruolo = UserRole.USER;
        if (!string.IsNullOrWhiteSpace(request.Ruolo) &&
            Enum.TryParse<UserRole>(request.Ruolo, ignoreCase: true, out var parsedRole))
        {
            ruolo = parsedRole;
        }

        var utente = new Utente
        {
            Nome = request.Nome,
            Cognome = request.Cognome,
            DataDiNascita = request.DataDiNascita,
            Email = request.Email,
            Username = request.Username,
            Password = passwords.Hash(request.Password),
            Ruolo = ruolo
        };

        db.Utenti.Add(utente);
        await db.SaveChangesAsync();

        var contenuto = "Ciao " + utente.Nome + ",\n\n"
            + "La registrazione è avvenuta  con successo!\n\n"
            + "Ora potrai effetturare prenotazioni per i seguenti servizi:\n\n"
            + "🎗️ CONSULENZA INIZIALE: Durante il primo incontro analizziamo insieme il tuo stato fisico attuale, i tuoi obiettivi e le tue abitudini di allenamento" + "\n"
            + "💆‍♂️ MASSAGGIO RILASSANTE: Un massaggio della durata di un'ora che aiuta a sciogliere le tensioni accumulate e favorisce un senso di benessere generale" + "\n"
            + "🏃 MASSAGGIO DECONTRATTURANTE: Un massaggio che aiuta a eliminare l'acido lattico e migliora la circolazione per un recupero più rapido dopo l'allenamento." + "\n\n"
            + "Se hai bisogno di altre informazioni non esitare a contattarmi:\n"
            + "📞 3772082714\n"
            + "📩 alessandrosaccomanni.pt@gmail.com\n\n"
            + "Grazie!\n"
            + "A presto, cordiali saluti,\n\nDott.Alessandro";

        await mail.InviaEmailAsync(utente.Email, "Registrazione avvenuta con successo", contenuto);

        return utente;
    }

    public Utente? VerificaCredenziali(string username, string password)
    {
        var utente = db.Utenti.AsNoTracking().FirstOrDefault(u => u.Username == username);
        if (utente is null || !passwords.Verify(password, utente.Password))
        {
            return null;
        }

        return utente;
    }

    public List<Utente> GetAll() => db.Utenti.AsNoTracking().OrderBy(u => u.Id).ToList();

    public Utente GetById(long id) =>
        db.Utenti.FirstOrDefault(u => u.Id == id) ?? throw new InvalidOperationException("Utente non trovato!⚠️");

    public async Task<Utente> UpdateAsync(long id, Utente input)
    {
        var utente = GetById(id);

        if (!string.IsNullOrWhiteSpace(input.Nome)) utente.Nome = input.Nome;
        if (!string.IsNullOrWhiteSpace(input.Cognome)) utente.Cognome = input.Cognome;
        if (input.DataDiNascita is not null) utente.DataDiNascita = input.DataDiNascita;
        if (!string.IsNullOrWhiteSpace(input.Username)) utente.Username = input.Username;
        if (!string.IsNullOrWhiteSpace(input.Email)) utente.Email = input.Email;
        if (!string.IsNullOrWhiteSpace(input.Password)) utente.Password = passwords.Hash(input.Password);
        if (input.Ruolo is not null) utente.Ruolo = input.Ruolo;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Username o email già presenti nel sistema");
        }

        return utente;
    }

    public async Task DeleteAsync(long id)
    {
        var utente = GetById(id);
        db.Prenotazioni.RemoveRange(db.Prenotazioni.Where(p => p.UtenteId == utente.Id));
        db.Utenti.Remove(utente);
        await db.SaveChangesAsync();
    }
}
