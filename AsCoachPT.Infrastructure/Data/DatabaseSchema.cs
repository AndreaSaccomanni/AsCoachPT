using Microsoft.EntityFrameworkCore;

namespace AsCoachPT.Infrastructure.Data;

/// <summary>
/// Adeguamenti dello schema per i database già esistenti. Il progetto usa
/// EnsureCreated (che non altera le tabelle), quindi le nuove colonne vengono
/// aggiunte qui in modo idempotente all'avvio.
/// </summary>
public static class DatabaseSchema
{
    public static async Task AssicuraAsync(AsCoachDbContext contesto, CancellationToken tokenAnnullamento = default)
    {
        var provider = contesto.Database.ProviderName ?? string.Empty;

        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            await contesto.Database.ExecuteSqlRawAsync(
                "ALTER TABLE indirizzi ADD COLUMN IF NOT EXISTS attivo boolean NOT NULL DEFAULT true;",
                tokenAnnullamento);
            return;
        }

        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var esiste = await contesto.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM pragma_table_info('indirizzi') WHERE name = 'attivo'")
                .FirstOrDefaultAsync(tokenAnnullamento);

            if (esiste == 0)
            {
                await contesto.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE indirizzi ADD COLUMN attivo INTEGER NOT NULL DEFAULT 1;",
                    tokenAnnullamento);
            }
        }
    }
}
