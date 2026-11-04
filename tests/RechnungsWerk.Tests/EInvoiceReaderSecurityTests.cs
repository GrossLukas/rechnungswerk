using System.IO.Compression;
using System.Text;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using RechnungsWerk.Core.EInvoice;
using RechnungsWerk.Core.Invoicing;
using RechnungsWerk.Core.Pdf;

namespace RechnungsWerk.Tests;

public sealed class EInvoiceReaderSecurityTests
{
    private readonly EInvoiceReader reader = new();

    [Fact]
    public void Xxe_file_is_rejected_without_reading_the_referenced_file()
    {
        var secretFile = Path.Combine(Path.GetTempPath(), $"rw-secret-{Guid.NewGuid():N}.txt");
        File.WriteAllText(secretFile, "GEHEIM-12345");
        try
        {
            var xml = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE Invoice [ <!ENTITY xxe SYSTEM "{new Uri(secretFile).AbsoluteUri}"> ]>
                <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                         xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
                  <cbc:ID>&xxe;</cbc:ID>
                </Invoice>
                """;

            var error = Assert.Throws<EInvoiceReadException>(() => reader.Read(Encoding.UTF8.GetBytes(xml)));

            Assert.Contains("DOCTYPE", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("GEHEIM", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(secretFile);
        }
    }

    [Fact]
    public void Entity_expansion_bomb_is_rejected()
    {
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE lolz [
              <!ENTITY lol "lol">
              <!ENTITY lol2 "&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;">
              <!ENTITY lol3 "&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;">
            ]>
            <lolz>&lol3;</lolz>
            """;

        Assert.Throws<EInvoiceReadException>(() => reader.Read(Encoding.UTF8.GetBytes(xml)));
    }

    [Fact]
    public void Deeply_nested_invoice_is_rejected_instead_of_overflowing_the_stack()
    {
        var error = Assert.Throws<EInvoiceReadException>(() => reader.Read(DeeplyNestedInvoice()));

        Assert.Contains("zu tief verschachtelt", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deeply_nested_invoice_is_not_passed_on_to_the_validator()
    {
        var error = Assert.Throws<EInvoiceReadException>(() => reader.ExtractXml(DeeplyNestedInvoice()));

        Assert.Contains("zu tief verschachtelt", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Doctype_in_utf16_file_is_detected()
    {
        var xml = "<?xml version=\"1.0\" encoding=\"UTF-16\"?><!DOCTYPE x [<!ENTITY a \"b\">]><x>&a;</x>";
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(xml)).ToArray();

        var error = Assert.Throws<EInvoiceReadException>(() => reader.Read(bytes));

        Assert.Contains("DOCTYPE", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "leer")]
    [InlineData("Das ist keine Rechnung", "kein gültiges XML")]
    [InlineData("<?xml version=\"1.0\"?><Bestellung><Nr>1</Nr></Bestellung>", "Unbekanntes Format")]
    public void Unsupported_content_gets_a_clear_message(string content, string expectedFragment)
    {
        var error = Assert.Throws<EInvoiceReadException>(() => reader.Read(Encoding.UTF8.GetBytes(content)));

        Assert.Contains(expectedFragment, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Files_over_10_mb_are_rejected()
    {
        var error = Assert.Throws<EInvoiceReadException>(() => reader.Read(new byte[EInvoiceReader.MaxFileBytes + 1]));

        Assert.Contains("10 MB", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pdf_without_embedded_invoice_explains_what_is_missing()
    {
        var document = TestInvoices.Sample();
        var plainPdf = new InvoicePdfRenderer().Render(document, InvoiceCalculator.Calculate(document));

        var error = Assert.Throws<EInvoiceReadException>(() => reader.Read(plainPdf));

        Assert.Contains("keine eingebetteten Rechnungsdaten", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Broken_pdf_is_reported_as_unreadable()
    {
        var error = Assert.Throws<EInvoiceReadException>(() => reader.Read(Encoding.ASCII.GetBytes("%PDF-1.7\nkaputt")));

        Assert.Contains("PDF konnte nicht geöffnet werden", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Embedded_files_that_unpack_to_more_than_30_mb_together_are_rejected()
    {
        // Jede Datei liegt unter der Grenze für eine einzelne Datei (20 MB), zusammen sind es 78 MB.
        var pdf = ZugferdWithAttachments(Enumerable.Range(1, 4).Select(i => ($"nullen-{i}.bin", new byte[19_500_000])));

        var error = Assert.Throws<EInvoiceReadException>(() => reader.Read(pdf));

        Assert.Contains("zusammen größer als 30 MB", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validation_unpacks_only_the_invoice_from_a_pdf_with_large_attachments()
    {
        var plain = new EInvoiceWriter().Write(TestInvoices.Sample(), EInvoiceFormat.ZugferdPdf);
        var pdf = ZugferdWithAttachments(Enumerable.Range(1, 4).Select(i => ($"nullen-{i}.bin", new byte[19_500_000])));

        Assert.Equal(reader.ExtractXml(plain), reader.ExtractXml(pdf));
    }

    [Fact]
    public void Further_embedded_files_are_listed_with_name_and_size()
    {
        var pdf = ZugferdWithAttachments([("lieferschein.txt", Encoding.UTF8.GetBytes(new string('x', 1234)))]);

        var attachment = Assert.Single(reader.Read(pdf).View.Attachments);

        Assert.Equal("lieferschein.txt", attachment.FileName);
        Assert.Equal(1234, attachment.Size);
        Assert.True(attachment.EmbeddedInPdf);
    }

    /// <summary>ZUGFeRD-PDF mit weiteren, per Flate komprimierten Dateien im /AF-Verzeichnis.</summary>
    private static byte[] ZugferdWithAttachments(IEnumerable<(string Name, byte[] Content)> files)
    {
        var pdf = new EInvoiceWriter().Write(TestInvoices.Sample(), EInvoiceFormat.ZugferdPdf);
        using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Modify);
        var associated = (PdfArray)((PdfReference)document.Internals.Catalog.Elements["/AF"]!).Value;
        foreach (var (name, content) in files)
        {
            using var compressed = new MemoryStream();
            using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                zlib.Write(content);
            }

            var stream = new PdfDictionary(document);
            stream.Elements["/Type"] = new PdfName("/EmbeddedFile");
            stream.Elements["/Filter"] = new PdfName("/FlateDecode");
            stream.CreateStream(compressed.ToArray());
            document.Internals.AddObject(stream);

            var embedded = new PdfDictionary(document);
            embedded.Elements["/F"] = stream.Reference;
            var spec = new PdfDictionary(document);
            spec.Elements["/Type"] = new PdfName("/Filespec");
            spec.Elements["/F"] = new PdfString(name);
            spec.Elements["/EF"] = embedded;
            document.Internals.AddObject(spec);
            associated.Elements.Add(spec.Reference!);
        }

        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }

    /// <summary>Gültige XRechnung, deren Hinweis 50.000 ineinander geschachtelte Elemente enthält.</summary>
    private static byte[] DeeplyNestedInvoice()
    {
        const int depth = 50_000;
        var xml = Encoding.UTF8.GetString(new EInvoiceWriter().Write(TestInvoices.Sample(), EInvoiceFormat.XRechnungUbl));
        var note = xml.IndexOf("<cbc:Note>", StringComparison.Ordinal) + "<cbc:Note>".Length;
        var nested = string.Concat(Enumerable.Repeat("<x>", depth)) + string.Concat(Enumerable.Repeat("</x>", depth));
        return Encoding.UTF8.GetBytes(xml.Insert(note, nested));
    }
}
