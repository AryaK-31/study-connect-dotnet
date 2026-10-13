using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace StudyConnect.Core.Security;

/// <summary>Hashes and verifies passwords.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string storedHash);
}

/// <summary>
/// PBKDF2-HMAC-SHA256 password hasher (replaces bcrypt from the Node.js version).
/// Format: <c>PBKDF2$iterations$saltBase64$keyBase64</c> - storing the iteration count lets us
/// raise it in future without invalidating existing hashes.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Prefix = "PBKDF2";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public Pbkdf2PasswordHasher()
        : this(100_000)
    {
    }

    public Pbkdf2PasswordHasher(int iterations)
    {
        if (iterations < 1_000)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), "Use at least 1,000 iterations.");
        }

        Iterations = iterations;
    }

    public int Iterations { get; }

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

        return string.Join('$',
            Prefix,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        string[] parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int iterations))
        {
            return false;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);

            // Constant-time comparison prevents timing attacks.
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>Creates and hashes the random tokens used in password-reset links.</summary>
public static class SecureTokens
{
    /// <summary>Creates a URL-safe random token (64 hex characters).</summary>
    public static string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    /// <summary>SHA-256 hash of a token; only the hash is stored in the database.</summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty))).ToLowerInvariant();
}
