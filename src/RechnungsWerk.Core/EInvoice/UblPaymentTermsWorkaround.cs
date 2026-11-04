using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace RechnungsWerk.Core.EInvoice;

/// <summary>
/// Umgehung für ZUGFeRD-csharp 18.0.1: Der UBL-Writer setzt vor jede Zahlungsbedingung einen Zeilenumbruch mit Einrückung und
/// dahinter noch einen Umbruch – beides wird Teil des Textes in cac:PaymentTerms/cbc:Note (BT-20). Übrig bleiben die Zeilen
/// selbst. Skonto-Zeilen (#SKONTO#…), nach denen laut BR-DE-18 ein Umbruch folgen muss, schreibt RechnungsWerk nicht.
/// </summary>
internal static class UblPaymentTermsWorkaround
{
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    /// <summary>Entfernt Ränder und Einrückung aus den Zahlungsbedingungen. Ist nichts zu tun, kommt dasselbe Array zurück.</summary>
    public static byte[] Apply(byte[] ubl)
    {
        var document = SecureXml.Load(ubl);
        var changed = false;
        foreach (var note in document.Descendants(Cac + "PaymentTerms").Elements(Cbc + "Note"))
        {
            var text = string.Join('\n', note.Value.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));
            if (text != note.Value)
            {
                note.Value = text;
                changed = true;
            }
        }

        if (!changed)
        {
            return ubl;
        }

        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
        {
            document.Save(writer);
        }

        return output.ToArray();
    }
}
