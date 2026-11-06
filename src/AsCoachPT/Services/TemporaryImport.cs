using System.Text.Json;
using AsCoachPT.Data;
using AsCoachPT.Domain;
using Microsoft.EntityFrameworkCore;

namespace AsCoachPT.Services;

// Endpoint temporaneo per il restore dei dati dal clone locale.
// Viene rimosso dopo l'import iniziale.
public static class TemporaryImport
{
    public static async Task<IResult> HandleAsync(HttpContext http, AsCoachDbContext db)
    {
        var token = Environment.GetEnvironmentVariable("IMPORT_TOKEN");
        if (string.IsNullOrWhiteSpace(token) || http.Request.Headers["X-Import-Token"] != token)
        {
            return Results.NotFound();
        }

        using var document = await JsonDocument.ParseAsync(http.Request.Body);

        db.Prenotazioni.RemoveRange(db.Prenotazioni);
        db.Indirizzi.RemoveRange(db.Indirizzi);
        db.Servizi.RemoveRange(db.Servizi);
        db.Utenti.RemoveRange(db.Utenti);
        db.Ruoli.RemoveRange(db.Ruoli);
        await db.SaveChangesAsync();

        foreach (var r in document.RootElement.GetProperty("ruoli").EnumerateArray())
        {
            db.Ruoli.Add(new Ruolo { Id = r.GetProperty("id").GetInt64(), Name = Enum.Parse<UserRole>(r.GetProperty("name").GetString()!) });
        }

        foreach (var u in document.RootElement.GetProperty("utenti").EnumerateArray())
        {
            var data = u.GetProperty("data_di_nascita");
            db.Utenti.Add(new Utente
            {
                Id = u.GetProperty("id").GetInt64(),
                Nome = u.GetProperty("nome").GetString()!,
                Cognome = u.GetProperty("cognome").GetString()!,
                DataDiNascita = data.ValueKind == JsonValueKind.Null ? null : DateOnly.Parse(data.GetString()!),
                Email = u.GetProperty("email").GetString()!,
                Username = u.GetProperty("username").GetString()!,
                Password = u.GetProperty("password").GetString()!,
                Ruolo = u.GetProperty("ruolo").ValueKind == JsonValueKind.Null ? null : Enum.Parse<UserRole>(u.GetProperty("ruolo").GetString()!)
            });
        }

        foreach (var i in document.RootElement.GetProperty("indirizzi").EnumerateArray())
        {
            db.Indirizzi.Add(new Indirizzo
            {
                Id = i.GetProperty("id").GetInt64(),
                Via = i.GetProperty("via").GetString() ?? string.Empty,
                NumeroCivico = i.GetProperty("numero_civico").GetString() ?? string.Empty,
                Citta = i.GetProperty("citta").GetString() ?? string.Empty,
                Provincia = i.GetProperty("provincia").GetString() ?? string.Empty,
                Latitudine = i.GetProperty("latitudine").ValueKind == JsonValueKind.Null ? null : i.GetProperty("latitudine").GetDouble(),
                Longitudine = i.GetProperty("longitudine").ValueKind == JsonValueKind.Null ? null : i.GetProperty("longitudine").GetDouble(),
                NomeStudio = i.GetProperty("nome_studio").GetString() ?? string.Empty,
                GiorniDisponibili = i.GetProperty("giorni_disponibili").GetString()
            });
        }

        foreach (var s in document.RootElement.GetProperty("servizi").EnumerateArray())
        {
            var id = s.GetProperty("id").GetInt64();
            var durata = s.GetProperty("durata").GetInt32();
            var tipo = s.GetProperty("servizio_type").GetString();

            if (tipo == "CONSULENZA")
            {
                db.Servizi.Add(new Consulenza
                {
                    Id = id,
                    Durata = durata,
                    TipoConsulenza = Enum.Parse<TipoConsulenza>(s.GetProperty("tipo_consulenza").GetString()!)
                });
            }
            else
            {
                db.Servizi.Add(new Massaggio
                {
                    Id = id,
                    Durata = durata,
                    TipoMassaggio = Enum.Parse<TipoMassaggio>(s.GetProperty("tipo_massaggio").GetString()!)
                });
            }
        }

        await db.SaveChangesAsync();

        foreach (var p in document.RootElement.GetProperty("prenotazioni").EnumerateArray())
        {
            db.Prenotazioni.Add(new Prenotazione
            {
                Id = p.GetProperty("id").GetInt64(),
                UtenteId = p.GetProperty("utente_id").GetInt64(),
                ServizioId = p.GetProperty("servizio_id").GetInt64(),
                IndirizzoId = p.GetProperty("indirizzo_id").GetInt64(),
                DataOra = DateTime.Parse(p.GetProperty("data_ora").GetString()!),
                Note = p.GetProperty("note").ValueKind == JsonValueKind.Null ? null : p.GetProperty("note").GetString()
            });
        }

        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('utenti','id'), (SELECT COALESCE(MAX(id),1) FROM utenti))");
        await db.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('indirizzi','id'), (SELECT COALESCE(MAX(id),1) FROM indirizzi))");
        await db.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('servizi','id'), (SELECT COALESCE(MAX(id),1) FROM servizi))");
        await db.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('prenotazioni','id'), (SELECT COALESCE(MAX(id),1) FROM prenotazioni))");
        await db.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('ruoli','id'), (SELECT COALESCE(MAX(id),1) FROM ruoli))");

        return Results.Ok(new
        {
            utenti = await db.Utenti.CountAsync(),
            servizi = await db.Servizi.CountAsync(),
            prenotazioni = await db.Prenotazioni.CountAsync(),
            indirizzi = await db.Indirizzi.CountAsync(),
            ruoli = await db.Ruoli.CountAsync()
        });
    }
}
