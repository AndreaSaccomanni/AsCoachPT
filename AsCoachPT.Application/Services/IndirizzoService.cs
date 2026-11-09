using AsCoachPT.Application.Repositories;
using AsCoachPT.Domain.Models;

namespace AsCoachPT.Application.Services;

/// <summary>Gestisce gli studi e i giorni disponibili per le prenotazioni.</summary>
public sealed class IndirizzoService(
    IIndirizzoRepository indirizzi,
    IPrenotazioneRepository prenotazioni,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Restituisce gli indirizzi in ordine di inserimento. Di default solo gli
    /// attivi: quelli disabilitati non compaiono nelle prenotazioni.
    /// </summary>
    public Task<List<Indirizzo>> ElencaAsync(bool includiDisabilitati = false, CancellationToken tokenAnnullamento = default) =>
        indirizzi.ElencaAsync(includiDisabilitati, tokenAnnullamento);

    /// <summary>
    /// Abilita o disabilita uno studio. Disabilitandolo i giorni tornano liberi
    /// (e possono essere assegnati a un altro indirizzo).
    /// </summary>
    public async Task<Indirizzo> ImpostaAttivoAsync(long id, bool attivo, CancellationToken tokenAnnullamento = default)
    {
        var indirizzo = await TrovaPerIdAsync(id, tokenAnnullamento);
        indirizzo.Attivo = attivo;

        if (!attivo)
        {
            indirizzo.GiorniDisponibili = string.Empty;
        }

        await unitOfWork.SalvaModificheAsync(tokenAnnullamento);
        return indirizzo;
    }

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
        var indirizzo = await TrovaPerIdAsync(id, tokenAnnullamento);

        if (indirizzo.Attivo)
        {
            await RimuoviGiorniGiaAssegnatiAsync(modifiche.Giorni, id, tokenAnnullamento);
        }

        indirizzo.Via = modifiche.Via;
        indirizzo.NumeroCivico = modifiche.NumeroCivico;
        indirizzo.Citta = modifiche.Citta;
        indirizzo.Provincia = modifiche.Provincia;
        indirizzo.NomeStudio = modifiche.NomeStudio;
        indirizzo.Latitudine = modifiche.Latitudine;
        indirizzo.Longitudine = modifiche.Longitudine;
        indirizzo.GiorniDisponibili = indirizzo.Attivo ? Normalizza(modifiche.Giorni) : string.Empty;

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

        var tuttiIndirizzi = await indirizzi.ElencaAsync(true, tokenAnnullamento);
        foreach (var altro in tuttiIndirizzi)
        {
            if (!altro.Attivo)
            {
                continue;
            }

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
