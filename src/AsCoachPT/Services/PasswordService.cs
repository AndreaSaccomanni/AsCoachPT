using AsCoachPT.Domain;

namespace AsCoachPT.Services;

public class PasswordService
{
    // BCrypt.Net verifies both legacy $2a$/$2y$ hashes (Spring BCryptPasswordEncoder) and new hashes.
    public string Hash(string plainPassword) => BCrypt.Net.BCrypt.HashPassword(plainPassword);

    public bool Verify(string plainPassword, string hash)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(plainPassword))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(plainPassword, hash);
        }
        catch (global::BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    public static AppUser ToAppUser(Utente utente) => new(
        utente.Id,
        utente.Nome,
        utente.Email,
        utente.Username,
        utente.Ruolo ?? UserRole.USER);
}
