using AsCoachPT.Application.Repositories;
using AsCoachPT.Domain.Models;

namespace AsCoachPT.Application.Services;

/// <summary>Gestisce gli studi e i giorni disponibili per le prenotazioni.</summary>
public sealed class IndirizzoService(
    IIndirizzoRepository indirizzi,
    IPrenotazioneRepository prenotazioni,
    IUnitOfWork unitOfWork)
{
    /// <summary>Restituisce tutti gli indirizzi in ordine di inserimento.</summary>
    public Task<List<Indirizzo>> ElencaAsync(CancellationToken tokenAnnullamento = default) =>
        indirizzi.ElencaAsync(tokenAnnullamento);

    /// <summary>Recupera un indirizzo o segnala che non esiste.</summary>
    public async Task<Indirizzo> TrovaPerIdAsync(long id, CancellationToken tokenAnnullamento = default) =>
        await indirizzi.TrovaPerIdAsync(id, tokenAnnullamento) ?? throw new InvalidOperationException("Indirizzo non trovato");

    /// <summary>Aggiunge un indirizzo liberando i giorni eventualmente assegnati ad altri studi.</summary>
    public async Task<Indirizzo> AggiungiAsync(Indirizzo indirizzo, CancellationToken tokenAnnullamento = default)
    {
        await RimuoviGiorniGiaAssegnatiAsync(indirizzo.Giorni, null, tokenAnnullamento);
        indirizzo.Id = 0;
        indirizzo.GiorniDisponibili = Normalizza(indirizzo.Giorni);
        await indirizzi.AggiungiAsync(indirizzo, tokenAnnullamento);
        await unitOfWork.SalvaModificheAsync(tokenAnnullamento);
        return indirizzo;
    }

    /// <summary>Aggiorna un indirizzo mantenendo un solo studio per giorno della settimana.</summary>
    public async Task<Indirizzo> AggiornaAsync(long id, Indirizzo modifiche, CancellationToken tokenAnnullamento = default)
    {
        await RimuoviGiorniGiaAssegnatiAsync(modifiche.Giorni, id, tokenAnnullamento);

        var indirizzo = await TrovaPerIdAsync(id, tokenAnnullamento);
        indirizzo.Via = modifiche.Via;
        indirizzo.NumeroCivico = modifiche.NumeroCivico;
        indirizzo.Citta = modifiche.Citta;
        indirizzo.Provincia = modifiche.Provincia;
        indirizzo.NomeStudio = modifiche.NomeStudio;
        indirizzo.Latitudine = modifiche.Latitudine;
        indirizzo.Longitudine = modifiche.Longitudine;
        indirizzo.GiorniDisponibili = Normalizza(modifiche.Giorni);

        await unitOfWork.SalvaModificheAsync(tokenAnnullamento);
        return indirizzo;
    }

    /// <summary>Elimina lo studio e tutte le prenotazioni collegate.</summary>
    public async Task EliminaAsync(long id, CancellationToken tokenAnnullamento = default)
    {
        var indirizzo = await TrovaPerIdAsync(id, tokenAnnullamento);

        var prenotazioniCollegate = await prenotazioni.ElencaPerIndirizzoAsync(id, tokenAnnullamento);
        foreach (var prenotazione in prenotazioniCollegate)
        {
            prenotazioni.Rimuovi(prenotazione);
        }

        indirizzo.GiorniDisponibili = string.Empty;
        indirizzi.Rimuovi(indirizzo);
        await unitOfWork.SalvaModificheAsync(tokenAnnullamento);
    }

    /// <summary>Restituisce le prenotazioni collegate a uno studio.</summary>
    public Task<List<Prenotazione>> ElencaPrenotazioniAsync(long indirizzoId, CancellationToken tokenAnnullamento = default) =>
        prenotazioni.ElencaPerIndirizzoAsync(indirizzoId, tokenAnnullamento);

    private static string Normalizza(IEnumerable<string> giorni) =>
        string.Join(", ", giorni.Select(giorno => giorno.Trim()).Where(giorno => giorno.Length > 0));

    /// <summary>Rimuove i giorni indicati dagli altri indirizzi: ogni giorno ha un solo studio.</summary>
    private async Task RimuoviGiorniGiaAssegnatiAsync(List<string> giorniAssegnati, long? idIndirizzoDaAggiornare, CancellationToken tokenAnnullamento)
    {
        if (giorniAssegnati.Count == 0)
        {
            return;
        }

        var tuttiIndirizzi = await indirizzi.ElencaAsync(tokenAnnullamento);
        foreach (var altro in tuttiIndirizzi)
        {
            if (idIndirizzoDaAggiornare is not null && altro.Id == idIndirizzoDaAggiornare)
            {
                continue;
            }

            var giorniAltro = altro.Giorni;
            var rimasti = giorniAltro.Where(giorno => !giorniAssegnati.Contains(giorno)).ToList();
            if (rimasti.Count != giorniAltro.Count)
            {
                altro.GiorniDisponibili = string.Join(", ", rimasti);
            }
        }
    }
}
