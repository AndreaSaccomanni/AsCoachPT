using AsCoachPT.Application.Services;

namespace AsCoachPT.Infrastructure.Services;

/// <summary>Codifica e verifica le password con BCrypt, compatibile con gli hash del backend Spring.</summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    /// <summary>Genera un hash BCrypt della password indicata.</summary>
    public string GeneraHash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    /// <summary>Verifica la password anche contro hash legacy con prefisso $2a$.</summary>
    public bool Verifica(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (global::BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
