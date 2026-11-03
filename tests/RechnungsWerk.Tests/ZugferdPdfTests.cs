using System.Text;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using RechnungsWerk.Core.EInvoice;
using RechnungsWerk.Core.Invoicing;
using RechnungsWerk.Core.Pdf;

namespace RechnungsWerk.Tests;

public sealed class ZugferdPdfTests
{
    private readonly EInvoiceWriter writer = new();

    [Fact]
    public void Zugferd_pdf_embeds_factur_x_xml_as_associated_file()
    {
        var pdf = writer.Write(TestInvoices.Sample(), EInvoiceFormat.ZugferdPdf);

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        var catalog = document.Internals.Catalog;

        var associated = Assert.IsType<PdfArray>(Resolve(catalog.Elements["/AF"]));
        var spec = Assert.IsType<PdfDictionary>(Resolve(Assert.Single(associated.Elements)));
        Assert.Equal("factur-x.xml", spec.Elements.GetString("/F"));
        Assert.Equal("/Alternative", spec.Elements.GetName("/AFRelationship"));

        var metadata = Assert.IsType<PdfDictionary>(Resolve(catalog.Elements["/Metadata"]));
        var xmp = Encoding.UTF8.GetString(metadata.Stream.Value);
        Assert.Contains("urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#", xmp, StringComparison.Ordinal);
        Assert.Contains("<pdfaid:part>3</pdfaid:part>", xmp, StringComparison.Ordinal);
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

    private static PdfItem? Resolve(PdfItem? item) => item is PdfSharp.Pdf.Advanced.PdfReference reference ? reference.Value : item;
}
