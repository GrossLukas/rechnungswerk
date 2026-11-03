using System.Globalization;

namespace RechnungsWerk.Core.Formatting;

/// <summary>Einheitliche deutsche Schreibweise für Beträge, Mengen und Datumsangaben.</summary>
public static class GermanFormat
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("de-DE");

    public static string Money(decimal amount, string? currency = "EUR") =>
        currency is null or "" or "EUR"
            ? amount.ToString("#,##0.00", Culture) + " €"
            : amount.ToString("#,##0.00", Culture) + " " + currency;

    public static string Money(decimal? amount, string? currency = "EUR") =>
        amount is null ? "" : Money(amount.Value, currency);

    /// <summary>Menge ohne überflüssige Nachkommastellen: 2 statt 2,0000, aber 1,5.</summary>
    public static string Quantity(decimal quantity) => quantity.ToString("#,##0.####", Culture);

    /// <summary>Preis mit mindestens zwei, höchstens vier Nachkommastellen.</summary>
    public static string UnitPrice(decimal price, string? currency = "EUR") =>
        price.ToString("#,##0.00##", Culture) + (currency is null or "" or "EUR" ? " €" : " " + currency);

    public static string Percent(decimal rate) => rate.ToString("0.##", Culture) + " %";

    public static string Date(DateOnly date) => date.ToString("dd.MM.yyyy", Culture);

    public static string Date(DateOnly? date) => date is null ? "" : Date(date.Value);

    /// <summary>IBAN in Vierergruppen, wie sie auf Papier gut lesbar ist.</summary>
    public static string Iban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            return "";
        }

        var compact = iban.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        return string.Join(' ', compact.Chunk(4).Select(c => new string(c)));
    }
}
