using System.Security.Cryptography;
using System.Text;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using RechnungsWerk.Core.EInvoice;
using RechnungsWerk.Core.Invoicing;
using RechnungsWerk.Core.Pdf;
using s2industries.ZUGFeRD;
using s2industries.ZUGFeRD.PDF;

namespace RechnungsWerk.Tests;

public sealed class ZugferdPdfTests
{
    private readonly EInvoiceWriter writer = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Zugferd_pdf_embeds_factur_x_xml_as_associated_file(bool servicePeriod)
    {
        var pdf = writer.Write(servicePeriod ? TestInvoices.WithServicePeriod() : TestInvoices.Sample(), EInvoiceFormat.ZugferdPdf);

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        var catalog = document.Internals.Catalog;

        var spec = FacturXSpec(catalog);
        Assert.Equal("factur-x.xml", spec.Elements.GetString("/F"));
        Assert.Equal("/Alternative", spec.Elements.GetName("/AFRelationship"));

        var metadata = Assert.IsType<PdfDictionary>(Resolve(catalog.Elements["/Metadata"]));
        var xmp = Encoding.UTF8.GetString(metadata.Stream.Value);
        Assert.Contains("urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#", xmp, StringComparison.Ordinal);
        Assert.Contains("<pdfaid:part>3</pdfaid:part>", xmp, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Embedded_xml_matches_size_and_checksum_of_its_file_entry(bool servicePeriod)
    {
        var pdf = writer.Write(servicePeriod ? TestInvoices.WithServicePeriod() : TestInvoices.Sample(), EInvoiceFormat.ZugferdPdf);

        using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        var files = Assert.IsType<PdfDictionary>(Resolve(FacturXSpec(document.Internals.Catalog).Elements["/EF"]));
        var embedded = Assert.IsType<PdfDictionary>(Resolve(files.Elements["/F"]));
        var xml = embedded.Stream.UnfilteredValue;
        var parameters = Assert.IsType<PdfDictionary>(Resolve(embedded.Elements["/Params"]));

        Assert.Equal(xml.Length, parameters.Elements.GetInteger("/Size"));
        Assert.Equal(MD5.HashData(xml), Encoding.Latin1.GetBytes(parameters.Elements.GetString("/CheckSum")));
        Assert.Equal(xml, new EInvoiceReader().ExtractXml(pdf));
    }

    [Fact]
    public void Repairing_the_embedded_xml_keeps_document_information_and_xmp_metadata()
    {
        var document = TestInvoices.WithServicePeriod();
        var calculation = InvoiceCalculator.Calculate(document);
        using var library = new MemoryStream();
        InvoicePdfProcessor.SaveToPdf(library, ZUGFeRDVersion.Version23, Profile.Comfort, ZUGFeRDFormats.CII,
            new MemoryStream(new InvoicePdfRenderer().Render(document, calculation)), EInvoiceWriter.BuildDescriptor(document, calculation));
        var original = library.ToArray();

        var repaired = CiiDeliveryWorkaround.ApplyToPdf(original);

        // PDF/A verlangt, dass Info-Verzeichnis und XMP zusammenpassen – die Reparatur darf daran nichts ändern.
        Assert.NotEqual(original, repaired);
        Assert.Equal(Metadata(original), Metadata(repaired));
    }

    [Fact]
    public void Embedded_xml_is_en16931_cii_with_the_invoice_data()
    {
        var pdf = writer.Write(TestInvoices.Sample(), EInvoiceFormat.ZugferdPdf);

        var xml = Encoding.UTF8.GetString(new EInvoiceReader().ExtractXml(pdf));

        Assert.Contains("CrossIndustryInvoice", xml, StringComparison.Ordinal);
        Assert.Contains("<ram:ID>urn:cen.eu:en16931:2017</ram:ID>", xml, StringComparison.Ordinal);
        Assert.Contains("RE-2026-0042", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Visible_pdf_uses_embedded_fonts_and_breaks_long_invoices_onto_more_pages()
    {
        var many = TestInvoices.Sample() with
        {
            Lines = Enumerable.Range(1, 60)
                .Select(i => new InvoiceLineItem { Name = $"Position {i} mit einer etwas längeren Bezeichnung", Quantity = i, UnitPrice = 9.99m, VatRate = 19m })
                .ToList(),
        };

        var pdf = new InvoicePdfRenderer().Render(many, InvoiceCalculator.Calculate(many));

        using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        Assert.True(document.PageCount >= 2);
        var text = Encoding.Latin1.GetString(pdf);
        Assert.Contains("/FontFile2", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Long_buyer_names_and_references_are_wrapped_instead_of_cut_off()
    {
        var sample = TestInvoices.Sample();
        var document = sample with
        {
            OrderReference = "BE-2026-11-04-000123456-KST",
            Buyer = sample.Buyer with
            {
                Name = "Landesamt für Umwelt, Naturschutz und Geologie Mecklenburg-Vorpommern",
                Street = "Abteilung Zentrale Dienste, Goldberger Straße 12–14, Haus B",
                LeitwegId = null,
                CustomerNumber = "K-2026-0000000000000000000000000000000000000001",
            },
            Lines = [new InvoiceLineItem { Name = "Beratung", Quantity = 1m, UnitCode = "C62", UnitPrice = 100m, VatRate = 19m }],
        };

        var pdf = new InvoicePdfRenderer().Render(document, InvoiceCalculator.Calculate(document));

        // Gekürzt wurde mit „…“ – steht das Zeichen nicht in der eingebetteten Schrift, ist nichts abgeschnitten.
        Assert.DoesNotContain('…', UsedCharacters(pdf));
    }

    /// <summary>Alle Zeichen, die das PDF laut ToUnicode-Tabellen seiner Schriften tatsächlich zeichnet.</summary>
    private static HashSet<char> UsedCharacters(byte[] pdf)
    {
        using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        var used = new HashSet<char>();
        foreach (var font in document.Internals.GetAllObjects().OfType<PdfDictionary>().Where(d => d.Elements.ContainsKey("/ToUnicode")))
        {
            var map = Encoding.ASCII.GetString(Assert.IsType<PdfDictionary>(Resolve(font.Elements["/ToUnicode"])).Stream.UnfilteredValue);
            foreach (System.Text.RegularExpressions.Match range in System.Text.RegularExpressions.Regex.Matches(
                         map, "<([0-9A-Fa-f]{4})>\\s*<([0-9A-Fa-f]{4})>\\s*<([0-9A-Fa-f]{4})>"))
            {
                var low = Convert.ToInt32(range.Groups[1].Value, 16);
                var high = Convert.ToInt32(range.Groups[2].Value, 16);
                var target = Convert.ToInt32(range.Groups[3].Value, 16);
                for (var glyph = low; glyph <= high; glyph++)
                {
                    used.Add((char)(target + glyph - low));
                }
            }
        }

        return used;
    }

    private static (string Info, string Xmp) Metadata(byte[] pdf)
    {
        using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        var info = string.Join("; ", document.Info.Elements.Keys.Order(StringComparer.Ordinal).Select(key => $"{key}={document.Info.Elements[key]}"));
        var metadata = Assert.IsType<PdfDictionary>(Resolve(document.Internals.Catalog.Elements["/Metadata"]));
        return (info, Encoding.UTF8.GetString(metadata.Stream.Value));
    }

    private static PdfDictionary FacturXSpec(PdfDictionary catalog)
    {
        var associated = Assert.IsType<PdfArray>(Resolve(catalog.Elements["/AF"]));
        return Assert.IsType<PdfDictionary>(Resolve(Assert.Single(associated.Elements)));
    }

    private static PdfItem? Resolve(PdfItem? item) => item is PdfSharp.Pdf.Advanced.PdfReference reference ? reference.Value : item;
}
