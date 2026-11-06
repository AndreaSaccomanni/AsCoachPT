namespace AsCoachPT.Application.Services;

/// <summary>Genera e verifica gli hash delle password degli utenti.</summary>
public interface IPasswordHasher
{
    /// <summary>Restituisce l'hash da salvare per la password indicata.</summary>
    string GeneraHash(string password);

    /// <summary>Verifica una password in chiaro contro l'hash salvato.</summary>
    bool Verifica(string password, string hash);
}
