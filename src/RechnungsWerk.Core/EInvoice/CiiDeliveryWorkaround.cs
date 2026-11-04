using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Filters;
using PdfSharp.Pdf.IO;

namespace RechnungsWerk.Core.EInvoice;

/// <summary>
/// Umgehung für ZUGFeRD-csharp 18.0.1: Dessen XML-Writer (ProfileAwareXmlTextWriter) lässt Elemente ohne Inhalt ganz weg –
/// auch ram:ApplicableHeaderTradeDelivery, das im CII-Schema Pflicht ist, aber leer sein darf. Leer ist es bei Rechnungen mit
/// Leistungszeitraum (BG-14 steht in der Abrechnung), wenn es weder Lieferdatum noch Lieferanschrift gibt; ohne das Element
/// verstößt die Datei gegen das Schema (cvc-complex-type.2.4.a). Ergänzt wird nur das leere Element, es entstehen keine
/// zusätzlichen Daten. Schreibt die Bibliothek das Element selbst, ändert sich nichts – dann kann diese Klasse weg.
/// </summary>
internal static class CiiDeliveryWorkaround
{
    private static readonly XNamespace Rsm = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";
    private static readonly XNamespace Ram = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";

    /// <summary>Ergänzt das leere Lieferelement vor der Abrechnung. Fehlt nichts, kommt dasselbe Array zurück.</summary>
    public static byte[] Apply(byte[] cii)
    {
        var document = SecureXml.Load(cii);
        var transaction = document.Root?.Element(Rsm + "SupplyChainTradeTransaction");
        var settlement = transaction?.Element(Ram + "ApplicableHeaderTradeSettlement");
        if (settlement is null || transaction!.Element(Ram + "ApplicableHeaderTradeDelivery") is not null)
        {
            return cii;
        }

        var delivery = new XElement(Ram + "ApplicableHeaderTradeDelivery");
        settlement.AddBeforeSelf(delivery);
        // Formatierung der Bibliothek beibehalten: Vor der Abrechnung steht wieder derselbe Zeilenumbruch mit Einzug.
        if (delivery.PreviousNode is XText { Value: var indentation } && string.IsNullOrWhiteSpace(indentation))
        {
            delivery.AddAfterSelf(new XText(indentation));
        }

        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
        {
            document.Save(writer);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Wendet <see cref="Apply"/> auf das eingebettete factur-x.xml eines ZUGFeRD-PDFs an und passt Größe und Prüfsumme des
    /// Anhangs an. Das PDF wird nur dann neu geschrieben, wenn sich das XML ändert; die XMP-Metadaten bleiben unverändert.
    /// </summary>
    public static byte[] ApplyToPdf(byte[] pdf)
    {
        using var document = PdfReader.Open(new MemoryStream(pdf, writable: false), PdfDocumentOpenMode.Modify);
        var file = EmbeddedInvoiceFile(document);
        var xml = file.Stream.UnfilteredValue;
        var fixedXml = Apply(xml);
        if (ReferenceEquals(fixedXml, xml))
        {
            return pdf;
        }

        file.Stream.Value = Filtering.FlateDecode.Encode(fixedXml);
        var parameters = Resolve(file.Elements["/Params"]) as PdfDictionary
            ?? throw new InvalidOperationException("Der Rechnungsanhang im ZUGFeRD-PDF hat keine /Params.");
        parameters.Elements["/Size"] = new PdfInteger(fixedXml.Length);
        // PDF verlangt für /CheckSum MD5; es dient nur der Erkennung von Übertragungsfehlern.
        parameters.Elements["/CheckSum"] = new PdfLiteral($"<{Convert.ToHexString(MD5.HashData(fixedXml))}>");

        // Die Bibliothek hat Factur-X- und PDF/A-3-Metadaten selbst geschrieben; PDFsharp soll sie nicht ersetzen. PDFsharp setzt
        // beim Öffnen zum Ändern /ModDate auf jetzt – das wird wieder entfernt, denn PDF/A verlangt, dass das Info-Verzeichnis
        // zu den unveränderten XMP-Daten passt.
        document.Options.ManualXmpGeneration = true;
        document.Info.Elements.Remove("/ModDate");
        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static PdfDictionary EmbeddedInvoiceFile(PdfDocument document)
    {
        var associated = Resolve(document.Internals.Catalog.Elements["/AF"]) as PdfArray;
        var spec = associated?.Elements.Select(Resolve).OfType<PdfDictionary>().SingleOrDefault();
        var files = Resolve(spec?.Elements["/EF"]) as PdfDictionary;
        return Resolve(files?.Elements["/F"]) as PdfDictionary is { Stream: not null } file
            ? file
            : throw new InvalidOperationException("Das ZUGFeRD-PDF enthält kein factur-x.xml als zugeordnete Datei (/AF).");
    }

    private static PdfItem? Resolve(PdfItem? item) => item is PdfReference reference ? reference.Value : item;
}
