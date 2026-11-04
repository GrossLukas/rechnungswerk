using System.IO.Compression;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace RechnungsWerk.Core.EInvoice;

/// <summary>Holt das Rechnungs-XML und weitere eingebettete Dateien aus einem ZUGFeRD-/Factur-X-PDF.</summary>
internal static class PdfXmlExtractor
{
    /// <summary>Obergrenze für eine entpackte eingebettete Datei (Schutz vor Zip-Bomben).</summary>
    public const int MaxEmbeddedFileBytes = 20 * 1024 * 1024;

    /// <summary>Obergrenze für alle entpackten Dateien zusammen – sonst ergeben viele kleine Bomben eine große.</summary>
    public const int MaxUnpackedBytes = 30 * 1024 * 1024;

    private const int MaxEmbeddedFiles = 50;
    private const int MaxTreeDepth = 8;

    private static readonly string[] InvoiceFileNames =
        ["factur-x.xml", "zugferd-invoice.xml", "xrechnung.xml", "zugferd.xml", "order-x.xml"];

    public static bool IsPdf(byte[] content)
    {
        var limit = Math.Min(content.Length, 1024) - 4;
        for (var i = 0; i < limit; i++)
        {
            if (content[i] == '%' && content[i + 1] == 'P' && content[i + 2] == 'D' && content[i + 3] == 'F' && content[i + 4] == '-')
            {
                return true;
            }
        }

        return false;
    }

    /// <param name="pdfBytes">Das hochgeladene PDF.</param>
    /// <param name="measureOtherFiles">
    /// Nur für die Ansicht: weitere Dateien entpacken, um ihre Größe zu zählen. Ihr Inhalt wird nicht behalten.
    /// </param>
    public static (byte[] Xml, IReadOnlyList<EmbeddedFile> OtherFiles) Extract(byte[] pdfBytes, bool measureOtherFiles)
    {
        PdfDocument document;
        try
        {
            document = PdfReader.Open(new MemoryStream(pdfBytes, writable: false), PdfDocumentOpenMode.Import);
        }
        catch (Exception ex)
        {
            throw new EInvoiceReadException("Das PDF konnte nicht geöffnet werden. Es ist beschädigt oder mit einem Passwort geschützt.", ex);
        }

        using (document)
        {
            var files = CollectFileSpecs(document).Select(ReadSpec).OfType<FileSpec>().ToList();
            if (files.Count == 0)
            {
                throw new EInvoiceReadException(
                    "Das PDF enthält keine eingebetteten Rechnungsdaten. Vermutlich ist es eine normale PDF-Rechnung ohne ZUGFeRD/Factur-X-XML – " +
                    "die kann RechnungsWerk nur als E-Rechnung lesen, wenn das XML enthalten ist.");
            }

            var unpacked = new UnpackedBytes();
            var sizes = new Dictionary<FileSpec, long>(ReferenceEqualityComparer.Instance);
            var invoice = files.FirstOrDefault(f => InvoiceFileNames.Contains(f.Name, StringComparer.OrdinalIgnoreCase));
            var xml = invoice is null ? null : Decode(invoice.Stream, unpacked);
            if (invoice is null)
            {
                foreach (var candidate in files.Where(f => f.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
                {
                    var content = Decode(candidate.Stream, unpacked);
                    if (LooksLikeInvoice(content))
                    {
                        (invoice, xml) = (candidate, content);
                        break;
                    }

                    sizes[candidate] = content.LongLength;
                }
            }

            if (invoice is null || xml is null)
            {
                throw new EInvoiceReadException(
                    "Das PDF enthält zwar Anhänge, aber keine Rechnungsdaten (factur-x.xml, zugferd-invoice.xml oder xrechnung.xml).");
            }

            IReadOnlyList<EmbeddedFile> others = measureOtherFiles
                ? files.Where(f => !ReferenceEquals(f, invoice))
                    .Select(f => new EmbeddedFile(f.Name, f.Description, f.MimeType,
                        sizes.TryGetValue(f, out var size) ? size : Unpack(f.Stream, null, unpacked)))
                    .ToList()
                : [];
            return (xml, others);
        }
    }

    public sealed record EmbeddedFile(string Name, string? Description, string? MimeType, long Size);

    private sealed record FileSpec(string Name, string? Description, string? MimeType, PdfDictionary Stream);

    /// <summary>Zählt, wie viele Bytes aus diesem PDF schon entpackt wurden – über alle eingebetteten Dateien.</summary>
    private sealed class UnpackedBytes
    {
        public long Total { get; set; }
    }

    private static bool LooksLikeInvoice(byte[] content)
    {
        var head = System.Text.Encoding.UTF8.GetString(content, 0, Math.Min(content.Length, 4096));
        return head.Contains("CrossIndustryInvoice", StringComparison.Ordinal)
               || head.Contains("CrossIndustryDocument", StringComparison.Ordinal)
               || head.Contains("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2", StringComparison.Ordinal)
               || head.Contains("urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2", StringComparison.Ordinal);
    }

    private static List<PdfDictionary> CollectFileSpecs(PdfDocument document)
    {
        var result = new List<PdfDictionary>();
        var seen = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        var catalog = document.Internals.Catalog;

        // Factur-X verlangt /AF im Katalog; ältere Dateien nutzen nur den EmbeddedFiles-Namensbaum.
        if (Resolve(catalog.Elements["/AF"]) is PdfArray associated)
        {
            foreach (var item in associated.Elements)
            {
                Add(Resolve(item) as PdfDictionary);
            }
        }

        var names = Resolve(catalog.Elements["/Names"]) as PdfDictionary;
        if (names is not null && Resolve(names.Elements["/EmbeddedFiles"]) is PdfDictionary tree)
        {
            WalkNameTree(tree, 0);
        }

        return result;

        void WalkNameTree(PdfDictionary node, int depth)
        {
            if (depth > MaxTreeDepth || result.Count >= MaxEmbeddedFiles)
            {
                return;
            }

            if (Resolve(node.Elements["/Names"]) is PdfArray pairs)
            {
                for (var i = 1; i < pairs.Elements.Count; i += 2)
                {
                    Add(Resolve(pairs.Elements[i]) as PdfDictionary);
                }
            }

            if (Resolve(node.Elements["/Kids"]) is PdfArray kids)
            {
                foreach (var kid in kids.Elements)
                {
                    if (Resolve(kid) is PdfDictionary child)
                    {
                        WalkNameTree(child, depth + 1);
                    }
                }
            }
        }

        void Add(PdfDictionary? spec)
        {
            if (spec is not null && result.Count < MaxEmbeddedFiles && seen.Add(spec) && spec.Elements.ContainsKey("/EF"))
            {
                result.Add(spec);
            }
        }
    }

    private static FileSpec? ReadSpec(PdfDictionary spec)
    {
        var name = (Resolve(spec.Elements["/UF"]) as PdfString)?.Value ?? (Resolve(spec.Elements["/F"]) as PdfString)?.Value;
        if (Resolve(spec.Elements["/EF"]) is not PdfDictionary ef)
        {
            return null;
        }

        var stream = (Resolve(ef.Elements["/F"]) ?? Resolve(ef.Elements["/UF"])) as PdfDictionary;
        if (stream?.Stream is null)
        {
            return null;
        }

        var description = (Resolve(spec.Elements["/Desc"]) as PdfString)?.Value;
        var subtype = (Resolve(stream.Elements["/Subtype"]) as PdfName)?.Value?.TrimStart('/').Replace("#2F", "/", StringComparison.OrdinalIgnoreCase);
        return new FileSpec(string.IsNullOrWhiteSpace(name) ? "anhang" : name.Trim(), description, subtype, stream);
    }

    private static byte[] Decode(PdfDictionary stream, UnpackedBytes unpacked)
    {
        using var buffer = new MemoryStream();
        Unpack(stream, buffer, unpacked);
        return buffer.ToArray();
    }

    /// <summary>Entpackt nach <paramref name="target"/> – ohne Ziel werden die Bytes nur gezählt.</summary>
    private static long Unpack(PdfDictionary stream, MemoryStream? target, UnpackedBytes unpacked)
    {
        var raw = stream.Stream.Value;
        var filters = Resolve(stream.Elements["/Filter"]) switch
        {
            null => [],
            PdfName filterName => [filterName.Value],
            PdfArray array => array.Elements.Select(e => (Resolve(e) as PdfName)?.Value ?? "?").ToArray(),
            _ => new[] { "?" },
        };

        if (filters.Length == 0)
        {
            return Copy(new MemoryStream(raw, writable: false), target, unpacked);
        }

        if (filters is ["/FlateDecode"] && !HasPredictor(stream))
        {
            return Inflate(raw, target, unpacked);
        }

        throw new EInvoiceReadException("Eine im PDF eingebettete Datei ist auf eine nicht unterstützte Weise komprimiert.");
    }

    private static bool HasPredictor(PdfDictionary stream) =>
        Resolve(stream.Elements["/DecodeParms"]) is PdfDictionary parms && parms.Elements.ContainsKey("/Predictor")
                                                                         && parms.Elements.GetInteger("/Predictor") > 1;

    private static long Inflate(byte[] raw, MemoryStream? target, UnpackedBytes unpacked)
    {
        try
        {
            return Copy(new ZLibStream(new MemoryStream(raw, writable: false), CompressionMode.Decompress), target, unpacked);
        }
        catch (InvalidDataException)
        {
            // Manche Programme schreiben rohes Deflate ohne zlib-Kopf.
            target?.SetLength(0);
            try
            {
                return Copy(new DeflateStream(new MemoryStream(raw, writable: false), CompressionMode.Decompress), target, unpacked);
            }
            catch (InvalidDataException ex)
            {
                throw new EInvoiceReadException("Eine im PDF eingebettete Datei ist beschädigt.", ex);
            }
        }
    }

    // Erst nach dem vollständigen Lesen mitgezählt, damit der zweite Versuch mit rohem Deflate nicht doppelt zählt.
    private static long Copy(Stream source, MemoryStream? target, UnpackedBytes unpacked)
    {
        using (source)
        {
            var chunk = new byte[81920];
            long length = 0;
            int read;
            while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
            {
                length += read;
                if (length > MaxEmbeddedFileBytes)
                {
                    throw new EInvoiceReadException(
                        $"Eine im PDF eingebettete Datei ist entpackt größer als {MaxEmbeddedFileBytes / (1024 * 1024)} MB und wird nicht verarbeitet.");
                }

                if (unpacked.Total + length > MaxUnpackedBytes)
                {
                    throw new EInvoiceReadException(
                        $"Die im PDF eingebetteten Dateien sind entpackt zusammen größer als {MaxUnpackedBytes / (1024 * 1024)} MB und werden nicht verarbeitet.");
                }

                target?.Write(chunk, 0, read);
            }

            unpacked.Total += length;
            return length;
        }
    }

    private static PdfItem? Resolve(PdfItem? item)
    {
        var depth = 0;
        while (item is PdfReference reference && depth++ < 16)
        {
            item = reference.Value;
        }

        return item;
    }
}
