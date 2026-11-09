namespace AsCoachPT.Domain.Models;

/// <summary>
/// Regole condivise sui giorni di ricevimento degli studi, salvati nella
/// proprietà <see cref="Indirizzo.GiorniDisponibili"/> con i nomi italiani.
/// </summary>
public static class GiorniStudio
{
    /// <summary>Giorni della settimana lavorativa usati dal sito (lunedì–sabato).</summary>
    public static readonly string[] Settimana = { "LUNEDI", "MARTEDI", "MERCOLEDI", "GIOVEDI", "VENERDI", "SABATO" };

    /// <summary>Converte il nome salvato a database nel giorno della settimana.</summary>
    public static DayOfWeek? DaNome(string nome) => Normalizza(nome) switch
    {
        "LUNEDI" => DayOfWeek.Monday,
        "MARTEDI" => DayOfWeek.Tuesday,
        "MERCOLEDI" => DayOfWeek.Wednesday,
        "GIOVEDI" => DayOfWeek.Thursday,
        "VENERDI" => DayOfWeek.Friday,
        "SABATO" => DayOfWeek.Saturday,
        "DOMENICA" => DayOfWeek.Sunday,
        _ => null
    };

    /// <summary>Nome salvato a database per un giorno.</summary>
    public static string Nome(DayOfWeek giorno) => giorno switch
    {
        DayOfWeek.Monday => "LUNEDI",
        DayOfWeek.Tuesday => "MARTEDI",
        DayOfWeek.Wednesday => "MERCOLEDI",
        DayOfWeek.Thursday => "GIOVEDI",
        DayOfWeek.Friday => "VENERDI",
        DayOfWeek.Saturday => "SABATO",
        _ => "DOMENICA"
    };

    /// <summary>Nome leggibile (con accento) per i messaggi in pagina.</summary>
    public static string NomeItaliano(DayOfWeek giorno) => giorno switch
    {
        DayOfWeek.Monday => "Lunedì",
        DayOfWeek.Tuesday => "Martedì",
        DayOfWeek.Wednesday => "Mercoledì",
        DayOfWeek.Thursday => "Giovedì",
        DayOfWeek.Friday => "Venerdì",
        DayOfWeek.Saturday => "Sabato",
        _ => "Domenica"
    };

    /// <summary>Uniforma maiuscole e accenti dei nomi provenienti dal database.</summary>
    public static string Normalizza(string testo) => testo.Trim().ToUpperInvariant()
        .Replace('À', 'A')
        .Replace('È', 'E')
        .Replace('É', 'E')
        .Replace('Ì', 'I')
        .Replace('Ò', 'O')
        .Replace('Ù', 'U');

    /// <summary>Vero se lo studio riceve in quel giorno.</summary>
    public static bool StudioAperto(Indirizzo studio, DayOfWeek giorno) =>
        studio.Giorni.Any(nome => DaNome(nome) == giorno);

    /// <summary>Tutti i giorni in cui almeno uno studio riceve.</summary>
    public static HashSet<DayOfWeek> DaStudi(IEnumerable<Indirizzo> studi)
    {
        var giorni = new HashSet<DayOfWeek>();

        foreach (var studio in studi)
        {
            foreach (var nome in studio.Giorni)
            {
                var giorno = DaNome(nome);
                if (giorno is not null)
                {
                    giorni.Add(giorno.Value);
                }
            }
        }

        return giorni;
    }

    /// <summary>Elenco leggibile dei giorni di ricevimento, ordinato.</summary>
    public static string ElencoLeggibile(IEnumerable<DayOfWeek> giorni) =>
        string.Join(", ", giorni.OrderBy(g => (int)g).Select(NomeItaliano));
}
