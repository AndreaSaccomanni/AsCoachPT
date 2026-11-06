using AsCoachPT.Data;
using AsCoachPT.Domain;
using Microsoft.EntityFrameworkCore;

namespace AsCoachPT.Services;

public class IndirizzoService(AsCoachDbContext db)
{
    public List<Indirizzo> GetAll() => db.Indirizzi.AsNoTracking().OrderBy(i => i.Id).ToList();

    public Indirizzo GetById(long id) =>
        db.Indirizzi.FirstOrDefault(i => i.Id == id) ?? throw new InvalidOperationException("Indirizzo non trovato");

    public async Task<Indirizzo> AddAsync(Indirizzo indirizzo)
    {
        RimuoviGiorniGiaAssegnati(indirizzo.Giorni, null);
        indirizzo.Id = 0;
        indirizzo.GiorniDisponibili = Normalizza(indirizzo.Giorni);
        db.Indirizzi.Add(indirizzo);
        await db.SaveChangesAsync();
        return indirizzo;
    }

    public async Task<Indirizzo> UpdateAsync(long id, Indirizzo input)
    {
        RimuoviGiorniGiaAssegnati(input.Giorni, id);

        var indirizzo = GetById(id);
        indirizzo.Via = input.Via;
        indirizzo.NumeroCivico = input.NumeroCivico;
        indirizzo.Citta = input.Citta;
        indirizzo.Provincia = input.Provincia;
        indirizzo.NomeStudio = input.NomeStudio;
        indirizzo.Latitudine = input.Latitudine;
        indirizzo.Longitudine = input.Longitudine;
        indirizzo.GiorniDisponibili = Normalizza(input.Giorni);

        await db.SaveChangesAsync();
        return indirizzo;
    }

    public async Task DeleteAsync(long id)
    {
        var indirizzo = GetById(id);
        db.Prenotazioni.RemoveRange(db.Prenotazioni.Where(p => p.IndirizzoId == id));
        indirizzo.GiorniDisponibili = string.Empty;
        await db.SaveChangesAsync();
        db.Indirizzi.Remove(indirizzo);
        await db.SaveChangesAsync();
    }

    public List<Prenotazione> PrenotazioniByIndirizzo(long indirizzoId) =>
        db.Prenotazioni.AsNoTracking()
            .Include(p => p.Utente)
            .Include(p => p.Servizio)
            .Include(p => p.Indirizzo)
            .Where(p => p.IndirizzoId == indirizzoId)
            .ToList();

    private static string Normalizza(IEnumerable<string> giorni) =>
        string.Join(", ", giorni.Select(g => g.Trim()).Where(g => g.Length > 0));

    // Un giorno della settimana può essere assegnato a un solo indirizzo.
    private void RimuoviGiorniGiaAssegnati(List<string> giorniAssegnati, long? idIndirizzoDaAggiornare)
    {
        if (giorniAssegnati.Count == 0)
        {
            return;
        }

        foreach (var altro in db.Indirizzi.ToList())
        {
            if (idIndirizzoDaAggiornare is not null && altro.Id == idIndirizzoDaAggiornare)
            {
                continue;
            }

            var giorniAltro = altro.Giorni;
            var rimasti = giorniAltro.Where(g => !giorniAssegnati.Contains(g)).ToList();
            if (rimasti.Count != giorniAltro.Count)
            {
                altro.GiorniDisponibili = string.Join(", ", rimasti);
            }
        }
    }
}
