using Microsoft.AspNetCore.Components;

namespace AsCoachPT.UI.Services;

/// <summary>
/// Costruisce l'indirizzo pubblico da inserire nei link inviati per email.
/// Usa App:PublicBaseUrl o la variabile PUBLIC_BASE_URL quando presenti, così il link
/// non dipende dall'host con cui il server è stato raggiunto (proxy, porta interna, sviluppo).
/// </summary>
public static class PublicUrl
{
    /// <summary>
    /// Restituisce la base pubblica configurata, senza barra finale; se non è
    /// configurata usa l'host da cui è stata aperta la pagina.
    /// </summary>
    /// <param name="configurazione">Configurazione dell'applicazione.</param>
    /// <param name="navigazione">Navigazione corrente, usata come fallback.</param>
    public static string Resolve(IConfiguration configurazione, NavigationManager navigazione)
    {
        var configurato = configurazione["App:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(configurato))
        {
            configurato = Environment.GetEnvironmentVariable("PUBLIC_BASE_URL");
        }

        return (string.IsNullOrWhiteSpace(configurato) ? navigazione.BaseUri : configurato).TrimEnd('/');
    }
}
