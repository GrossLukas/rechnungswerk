using System.Security.Cryptography;
using System.Text;

namespace RechnungsWerk.Web.Infrastructure;

/// <summary>
/// Einmaliger Einrichtungscode für die Ersteinrichtung. Er steht nur in einer Datei im Datenordner, damit niemand,
/// der eine frisch gestartete Instanz zufällig im Netz findet, das erste Admin-Konto anlegen kann.
/// Der Code selbst wird nie protokolliert.
/// </summary>
public sealed class SetupCodeStore(DataPaths paths, ILogger<SetupCodeStore> logger)
{
    // Crockford-Base32 ohne leicht verwechselbare Zeichen (I, L, O, U).
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int CodeLength = 20;

    private readonly Lock gate = new();

    public string FilePath => paths.SetupCode;

    public void EnsureCreated()
    {
        lock (gate)
        {
            if (ReadCode() is null)
            {
                var code = Generate();
                var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
                if (!OperatingSystem.IsWindows())
                {
                    options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                }

                using var writer = new StreamWriter(FilePath, Encoding.ASCII, options);
                writer.WriteLine(Format(code));
            }
        }
    }

    /// <summary>Nennt im Log nur den Ort der Datei, nie den Code selbst.</summary>
    public void AnnounceLocation() =>
        logger.LogWarning("Ersteinrichtung offen: Den Einrichtungscode findest du in der Datei {SetupCodeFile}.", FilePath);

    public bool Matches(string? input)
    {
        string? expected;
        lock (gate)
        {
            expected = ReadCode();
        }

        if (expected is null || string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Normalize(input)), Encoding.ASCII.GetBytes(expected));
    }

    public void Delete()
    {
        lock (gate)
        {
            File.Delete(FilePath);
        }
    }

    private string? ReadCode()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        var code = Normalize(File.ReadAllText(FilePath));
        return code.Length == CodeLength ? code : null;
    }

    private static string Generate() => RandomNumberGenerator.GetString(Alphabet, CodeLength);

    private static string Format(string code) => string.Join('-', code.Chunk(5).Select(chunk => new string(chunk)));

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var raw in value.ToUpperInvariant())
        {
            // Beim Abtippen verwechselte Zeichen wie in Crockford-Base32 üblich zuordnen.
            var c = raw switch
            {
                'O' => '0',
                'I' or 'L' => '1',
                _ => raw,
            };

            if (Alphabet.Contains(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
