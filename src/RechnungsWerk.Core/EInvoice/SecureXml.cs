using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace RechnungsWerk.Core.EInvoice;

/// <summary>
/// XML wird nur ohne DTD gelesen: keine externen Entitäten (XXE), keine Entitäten-Bomben, kein Nachladen aus dem Netz.
/// </summary>
public static class SecureXml
{
    public const int MaxCharacters = 30_000_000;

    /// <summary>
    /// Echte E-Rechnungen kommen mit gut zehn Ebenen aus. Tiefer verschachtelte Dateien würden die ZUGFeRD-Bibliothek,
    /// die Texte rekursiv liest, in einen Stapelüberlauf treiben – und der beendet den ganzen Prozess.
    /// </summary>
    public const int MaxDepth = 64;

    public static XmlReaderSettings ReaderSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersFromEntities = 0,
        MaxCharactersInDocument = MaxCharacters,
        IgnoreProcessingInstructions = true,
        CloseInput = true,
    };

    /// <exception cref="EInvoiceReadException">bei DTD, kaputtem XML, zu großen oder zu tief verschachtelten Dokumenten.</exception>
    public static XDocument Load(byte[] xml)
    {
        if (ContainsDoctype(xml))
        {
            throw new EInvoiceReadException(
                "Die Datei enthält eine Dokumenttyp-Definition (DOCTYPE). Aus Sicherheitsgründen werden solche Dateien nicht verarbeitet. " +
                "Echte E-Rechnungen enthalten keine DTD.");
        }

        try
        {
            EnsureDepth(xml);
            using var reader = XmlReader.Create(new MemoryStream(xml, writable: false), ReaderSettings());
            return XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException ex) when (ex.Message.Contains("DTD", StringComparison.OrdinalIgnoreCase))
        {
            throw new EInvoiceReadException("Die Datei enthält eine Dokumenttyp-Definition (DOCTYPE) und wird aus Sicherheitsgründen nicht verarbeitet.", ex);
        }
        catch (XmlException ex)
        {
            throw new EInvoiceReadException(
                $"Die Datei ist kein gültiges XML (Zeile {ex.LineNumber}, Spalte {ex.LinePosition}). Ist es vielleicht eine andere Dateiart?", ex);
        }
    }

    // Ein eigener Durchgang mit dem XmlReader liest Knoten für Knoten, ohne Rekursion und ohne Baum im Speicher.
    private static void EnsureDepth(byte[] xml)
    {
        using var reader = XmlReader.Create(new MemoryStream(xml, writable: false), ReaderSettings());
        while (reader.Read())
        {
            if (reader.Depth > MaxDepth)
            {
                throw new EInvoiceReadException(
                    $"Die Datei ist zu tief verschachtelt (mehr als {MaxDepth} Ebenen) und wird nicht verarbeitet.");
            }
        }
    }

    /// <summary>Schnelle Vorprüfung, damit DTDs eine eindeutige Meldung bekommen – auch in UTF-16-Dateien.</summary>
    internal static bool ContainsDoctype(byte[] xml)
    {
        var length = Math.Min(xml.Length, 64 * 1024);
        string head;
        if (length >= 2 && ((xml[0] == 0xFF && xml[1] == 0xFE) || (xml[0] == 0xFE && xml[1] == 0xFF)))
        {
            var encoding = xml[0] == 0xFF ? Encoding.Unicode : Encoding.BigEndianUnicode;
            head = encoding.GetString(xml, 2, length - 2);
        }
        else
        {
            head = Encoding.UTF8.GetString(xml, 0, length);
        }

        return head.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
               || head.Contains("<!ENTITY", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Fehler mit einer verständlichen deutschen Meldung, die direkt angezeigt werden darf.</summary>
public sealed class EInvoiceReadException : Exception
{
    public EInvoiceReadException(string message) : base(message)
    {
    }

    public EInvoiceReadException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
