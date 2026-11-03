using System.Numerics;

namespace RechnungsWerk.Core.Invoicing;

/// <summary>Prüfung von IBANs nach ISO 13616 (Prüfziffer Modulo 97).</summary>
public static class Iban
{
    public static string Normalize(string? iban) =>
        (iban ?? "").Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

    public static bool IsValid(string? iban)
    {
        var value = Normalize(iban);
        if (value.Length is < 15 or > 34 || !value.All(char.IsAsciiLetterOrDigit)
            || !char.IsAsciiLetter(value[0]) || !char.IsAsciiLetter(value[1])
            || !char.IsAsciiDigit(value[2]) || !char.IsAsciiDigit(value[3]))
        {
            return false;
        }

        if (value.StartsWith("DE", StringComparison.Ordinal) && value.Length != 22)
        {
            return false;
        }

        // Ländercode und Prüfziffer ans Ende, Buchstaben als Zahlen (A = 10 … Z = 35).
        var rearranged = value[4..] + value[..4];
        var digits = string.Concat(rearranged.Select(c => char.IsAsciiDigit(c) ? c.ToString() : (c - 'A' + 10).ToString()));
        return BigInteger.Parse(digits) % 97 == 1;
    }
}
