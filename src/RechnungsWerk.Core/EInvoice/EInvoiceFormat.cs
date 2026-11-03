using System.Text;

namespace RechnungsWerk.Core.EInvoice;

public enum EInvoiceFormat
{
    /// <summary>XRechnung 3.0 in UBL-Syntax – Standard für Behörden.</summary>
    XRechnungUbl,

    /// <summary>XRechnung 3.0 in CII-Syntax.</summary>
    XRechnungCii,

    /// <summary>ZUGFeRD 2.3 / Factur-X, Profil EN 16931: PDF/A-3 mit eingebettetem CII-XML.</summary>
    ZugferdPdf,
}

public static class EInvoiceFormats
{
    public static string Label(this EInvoiceFormat format) => format switch
    {
        EInvoiceFormat.XRechnungUbl => "XRechnung 3.0 (UBL)",
        EInvoiceFormat.XRechnungCii => "XRechnung 3.0 (CII)",
        EInvoiceFormat.ZugferdPdf => "ZUGFeRD / Factur-X (PDF)",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static string Hint(this EInvoiceFormat format) => format switch
    {
        EInvoiceFormat.XRechnungUbl => "Reines XML – Standard für Behörden und die meisten Rechnungsportale.",
        EInvoiceFormat.XRechnungCii => "Reines XML in der zweiten zulässigen Syntax.",
        EInvoiceFormat.ZugferdPdf => "PDF zum Ansehen mit eingebetteten Rechnungsdaten – beliebt bei Firmenkunden.",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static string ContentType(this EInvoiceFormat format) =>
        format == EInvoiceFormat.ZugferdPdf ? "application/pdf" : "application/xml";

    public static string FileName(this EInvoiceFormat format, string invoiceNumber)
    {
        var safe = SafeFileStem(invoiceNumber);
        return format switch
        {
            EInvoiceFormat.XRechnungUbl => $"{safe}_xrechnung.xml",
            EInvoiceFormat.XRechnungCii => $"{safe}_xrechnung-cii.xml",
            EInvoiceFormat.ZugferdPdf => $"{safe}.pdf",
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    /// <summary>Dateiname ohne Zeichen, die in Downloads oder Mail-Anhängen Ärger machen.</summary>
    public static string SafeFileStem(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value.Trim())
        {
            builder.Append(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_');
        }

        var result = builder.ToString().Trim('.', '_');
        return result.Length == 0 ? "rechnung" : result;
    }

    public static bool TryParse(string? value, out EInvoiceFormat format) =>
        Enum.TryParse(value, ignoreCase: true, out format) && Enum.IsDefined(format);
}
