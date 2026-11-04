using System.Text.RegularExpressions;

namespace RechnungsWerk.Core.Mail;

/// <param name="InvoiceNumber">Nummer der Rechnung.</param>
/// <param name="CustomerName">Name des Kunden.</param>
/// <param name="Amount">Gesamtbetrag, schon formatiert.</param>
/// <param name="DueDate">Fälligkeitsdatum, schon formatiert; ohne Fälligkeit <c>null</c>.</param>
/// <param name="CompanyName">Name der eigenen Firma.</param>
public sealed record MailTemplateValues(string InvoiceNumber, string CustomerName, string Amount, string? DueDate, string CompanyName);

/// <summary>Ersetzt Platzhalter wie {Rechnungsnummer} in Betreff und Text. Unbekannte Platzhalter bleiben stehen.</summary>
public static partial class MailTemplate
{
    public const string DefaultSubject = "Rechnung {Rechnungsnummer} von {Firma}";

    public const string DefaultBody =
        """
        Guten Tag,

        anbei erhalten Sie unsere Rechnung {Rechnungsnummer} über {Betrag}.
        Bitte überweisen Sie den Betrag bis zum {Fälligkeit}.

        Vielen Dank für Ihren Auftrag!

        Mit freundlichen Grüßen
        {Firma}
        """;

    public static readonly IReadOnlyList<(string Placeholder, string Description)> Placeholders =
    [
        ("{Rechnungsnummer}", "Nummer der Rechnung"),
        ("{Kunde}", "Name des Kunden"),
        ("{Betrag}", "Gesamtbetrag, z. B. 1.190,00 €"),
        ("{Fälligkeit}", "Fälligkeitsdatum, z. B. 17.11.2026"),
        ("{Firma}", "Name deiner Firma"),
    ];

    public static string Render(string template, MailTemplateValues values)
    {
        // Ohne Fälligkeit wird aus „bis zum {Fälligkeit}“ „sofort“ – beim Rendern, damit es auch für gespeicherte Vorlagen gilt.
        var text = values.DueDate is null ? UntilDueDate().Replace(template, Immediately) : template;
        return PlaceholderPattern().Replace(text, match => match.Groups[1].Value.ToLowerInvariant() switch
        {
            "rechnungsnummer" => values.InvoiceNumber,
            "kunde" => values.CustomerName,
            "betrag" => values.Amount,
            "fälligkeit" or "faelligkeit" => values.DueDate ?? Immediately,
            "firma" => values.CompanyName,
            _ => match.Value,
        });
    }

    /// <summary>Betreffzeilen dürfen keine Zeilenumbrüche enthalten (sonst ließen sich Kopfzeilen einschleusen).</summary>
    public static string RenderSubject(string template, MailTemplateValues values) =>
        LineBreaks().Replace(Render(template, values), " ").Trim();

    private const string Immediately = "sofort";

    [GeneratedRegex(@"\{(\p{L}+)\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"\bbis(?:\s+zum)?\s+\{f(?:ä|ae)lligkeit\}", RegexOptions.IgnoreCase)]
    private static partial Regex UntilDueDate();

    [GeneratedRegex(@"[\r\n]+")]
    private static partial Regex LineBreaks();
}
