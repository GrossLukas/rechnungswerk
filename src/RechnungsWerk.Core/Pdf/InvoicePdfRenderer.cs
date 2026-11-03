using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using RechnungsWerk.Core.Formatting;
using RechnungsWerk.Core.Invoicing;

namespace RechnungsWerk.Core.Pdf;

/// <summary>
/// Erzeugt das sichtbare Rechnungs-PDF (A4, Anschriftfeld nach DIN 5008 Form B). In dieses PDF bettet
/// <see cref="EInvoice.EInvoiceWriter"/> anschließend das ZUGFeRD-XML ein.
/// </summary>
public sealed class InvoicePdfRenderer
{
    public byte[] Render(InvoiceDocument document, InvoiceCalculation calculation)
    {
        EmbeddedFontResolver.EnsureRegistered();

        using var pdf = new PdfDocument();
        pdf.Info.Title = $"Rechnung {document.Number}";
        pdf.Info.Author = document.Seller.Name;
        pdf.Info.Subject = $"Rechnung {document.Number} an {document.Buyer.Name}";
        pdf.Info.Creator = "RechnungsWerk";

        new Layout(pdf, document, calculation).Render();

        using var output = new MemoryStream();
        pdf.Save(output, false);
        return output.ToArray();
    }

    private sealed class Layout(PdfDocument pdf, InvoiceDocument doc, InvoiceCalculation calc)
    {
        private const double Mm = 72 / 25.4;
        private const double Left = 25 * Mm;
        private const double Right = 190 * Mm;
        private const double ContentBottom = 266 * Mm;
        private const double Width = Right - Left;

        private static readonly XPdfFontOptions FontOptions = new(PdfFontEncoding.Unicode);
        private static readonly XColor Ink = XColor.FromArgb(31, 41, 55);
        private static readonly XColor Muted = XColor.FromArgb(100, 108, 122);
        private static readonly XColor Accent = XColor.FromArgb(15, 98, 254);
        private static readonly XColor Rule = XColor.FromArgb(222, 226, 232);
        private static readonly XColor HeaderFill = XColor.FromArgb(243, 245, 248);

        private readonly XBrush ink = new XSolidBrush(Ink);
        private readonly XBrush muted = new XSolidBrush(Muted);
        private readonly List<PdfPage> pages = [];
        private readonly bool showVat = !doc.Seller.IsSmallBusiness;

        private PdfPage page = null!;
        private XGraphics gfx = null!;
        private double y;

        private static XFont Font(double size, bool bold = false) =>
            new(EmbeddedFontResolver.FamilyName, size, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular, FontOptions);

        public void Render()
        {
            NewPage(first: true);
            DrawLetterHead();
            DrawAddressField();
            DrawInfoBlock();
            DrawTitleAndNote();
            DrawTable();
            DrawTotals();
            DrawClosingText();
            gfx.Dispose();
            DrawFooters();
        }

        private void NewPage(bool first)
        {
            if (!first)
            {
                gfx.Dispose();
            }

            page = pdf.AddPage();
            page.Size = PageSize.A4;
            pages.Add(page);
            gfx = XGraphics.FromPdfPage(page);
            y = first ? 0 : 20 * Mm;

            if (!first)
            {
                Text($"Rechnung {doc.Number} · Fortsetzung", Font(8), muted, Left, y);
                y += 8 * Mm;
            }
        }

        private void EnsureSpace(double height, bool repeatTableHeader)
        {
            if (y + height <= ContentBottom)
            {
                return;
            }

            NewPage(first: false);
            if (repeatTableHeader)
            {
                DrawTableHeader();
            }
        }

        private void DrawLetterHead()
        {
            Text(doc.Seller.Name, Font(15, bold: true), ink, Left, 15 * Mm);
            var contact = Join(" · ", doc.Seller.Street, $"{doc.Seller.PostalCode} {doc.Seller.City}", doc.Seller.Phone, doc.Seller.Email);
            Text(contact, Font(8), muted, Left, 22.5 * Mm);
            gfx.DrawLine(new XPen(Accent, 1.2), Left, 29 * Mm, Right, 29 * Mm);
        }

        private void DrawAddressField()
        {
            var sender = Join(" · ", doc.Seller.Name, doc.Seller.Street, $"{doc.Seller.PostalCode} {doc.Seller.City}");
            var senderFont = Font(7);
            Text(Fit(sender, senderFont, 85 * Mm), senderFont, muted, Left, 47 * Mm);
            gfx.DrawLine(new XPen(Rule, 0.5), Left, 51 * Mm, Left + 85 * Mm, 51 * Mm);

            var lines = new List<string> { doc.Buyer.Name };
            if (!string.IsNullOrWhiteSpace(doc.Buyer.ContactName))
            {
                lines.Add(doc.Buyer.ContactName!);
            }

            lines.Add(doc.Buyer.Street);
            lines.Add($"{doc.Buyer.PostalCode} {doc.Buyer.City}");
            if (!string.Equals(doc.Buyer.CountryCode, doc.Seller.CountryCode, StringComparison.OrdinalIgnoreCase))
            {
                lines.Add(CodeLabels.Country(doc.Buyer.CountryCode).ToUpperInvariant());
            }

            var font = Font(10);
            var lineY = 55 * Mm;
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                Text(Fit(line, font, 85 * Mm), font, ink, Left, lineY);
                lineY += 4.6 * Mm;
            }
        }

        private void DrawInfoBlock()
        {
            var rows = new List<(string Label, string Value)>
            {
                ("Rechnungsnummer", doc.Number),
                ("Rechnungsdatum", GermanFormat.Date(doc.IssueDate)),
            };

            if (doc.ServicePeriodStart is { } start && doc.ServicePeriodEnd is { } end)
            {
                rows.Add(("Leistungszeitraum", $"{GermanFormat.Date(start)} – {GermanFormat.Date(end)}"));
            }
            else if (doc.DeliveryDate is { } delivery)
            {
                rows.Add(("Leistungsdatum", GermanFormat.Date(delivery)));
            }

            if (!string.IsNullOrWhiteSpace(doc.Buyer.CustomerNumber))
            {
                rows.Add(("Kundennummer", doc.Buyer.CustomerNumber!));
            }

            if (!string.IsNullOrWhiteSpace(doc.Buyer.LeitwegId))
            {
                rows.Add(("Leitweg-ID", doc.Buyer.LeitwegId!));
            }
            else if (!string.IsNullOrWhiteSpace(doc.BuyerReference))
            {
                rows.Add(("Ihre Referenz", doc.BuyerReference!));
            }

            if (!string.IsNullOrWhiteSpace(doc.OrderReference))
            {
                rows.Add(("Bestellnummer", doc.OrderReference!));
            }

            if (doc.DueDate is { } due)
            {
                rows.Add(("Fällig am", GermanFormat.Date(due)));
            }

            var labelFont = Font(8.5);
            var valueFont = Font(8.5, bold: true);
            var rowY = 50 * Mm;
            foreach (var (label, value) in rows)
            {
                Text(label, labelFont, muted, 118 * Mm, rowY);
                TextRight(Fit(value, valueFont, 40 * Mm), valueFont, ink, Right, rowY);
                rowY += 5 * Mm;
            }
        }

        private void DrawTitleAndNote()
        {
            y = 100 * Mm;
            Text($"Rechnung {doc.Number}", Font(16, bold: true), ink, Left, y);
            y += 10 * Mm;

            if (!string.IsNullOrWhiteSpace(doc.Note))
            {
                var font = Font(9.5);
                foreach (var line in Wrap(doc.Note!, font, Width))
                {
                    EnsureSpace(5 * Mm, repeatTableHeader: false);
                    Text(line, font, ink, Left, y);
                    y += 4.6 * Mm;
                }

                y += 4 * Mm;
            }
        }

        // Spaltenpositionen in mm ab linkem Rand; ohne Steuerspalte (Kleinunternehmer) rücken die Zahlen nach rechts.
        private double ColPos => Left;
        private double ColName => Left + 9 * Mm;
        private double ColNameWidth => (showVat ? 62 : 74) * Mm;
        private double ColQtyRight => Left + (showVat ? 87 : 99) * Mm;
        private double ColUnit => Left + (showVat ? 89 : 101) * Mm;
        private double ColPriceRight => Left + (showVat ? 125 : 137) * Mm;
        private double ColVatRight => Left + 137 * Mm;
        private double ColAmountRight => Right;

        private void DrawTableHeader()
        {
            var font = Font(8, bold: true);
            gfx.DrawRectangle(new XSolidBrush(HeaderFill), Left, y, Width, 7 * Mm);
            var textY = y + 2.2 * Mm;
            Text("Pos.", font, muted, ColPos + 1.5 * Mm, textY);
            Text("Bezeichnung", font, muted, ColName, textY);
            TextRight("Menge", font, muted, ColQtyRight, textY);
            Text("Einheit", font, muted, ColUnit, textY);
            TextRight("Einzelpreis", font, muted, ColPriceRight, textY);
            if (showVat)
            {
                TextRight("USt.", font, muted, ColVatRight, textY);
            }

            TextRight("Betrag", font, muted, ColAmountRight - 1.5 * Mm, textY);
            y += 9 * Mm;
        }

        private void DrawTable()
        {
            EnsureSpace(20 * Mm, repeatTableHeader: false);
            DrawTableHeader();

            var nameFont = Font(9);
            var descriptionFont = Font(8);
            var numberFont = Font(9);
            var rule = new XPen(Rule, 0.5);

            foreach (var line in calc.Lines)
            {
                var nameLines = Wrap(line.Item.Name, nameFont, ColNameWidth).ToList();
                var descriptionLines = string.IsNullOrWhiteSpace(line.Item.Description)
                    ? []
                    : Wrap(line.Item.Description!, descriptionFont, ColNameWidth).ToList();
                var height = nameLines.Count * 4.3 * Mm + descriptionLines.Count * 3.8 * Mm + 3 * Mm;

                EnsureSpace(height, repeatTableHeader: true);

                Text(line.Position.ToString(GermanFormat.Culture), numberFont, muted, ColPos + 1.5 * Mm, y);
                TextRight(GermanFormat.Quantity(line.Item.Quantity), numberFont, ink, ColQtyRight, y);
                Text(Fit(UnitCodes.Label(line.Item.UnitCode), numberFont, 16 * Mm), numberFont, ink, ColUnit, y);
                TextRight(GermanFormat.UnitPrice(line.Item.UnitPrice, doc.Currency), numberFont, ink, ColPriceRight, y);
                if (showVat)
                {
                    TextRight(GermanFormat.Percent(line.VatRate), numberFont, ink, ColVatRight, y);
                }

                TextRight(GermanFormat.Money(line.NetAmount, doc.Currency), numberFont, ink, ColAmountRight - 1.5 * Mm, y);

                var rowY = y;
                foreach (var nameLine in nameLines)
                {
                    Text(nameLine, nameFont, ink, ColName, rowY);
                    rowY += 4.3 * Mm;
                }

                foreach (var descriptionLine in descriptionLines)
                {
                    Text(descriptionLine, descriptionFont, muted, ColName, rowY);
                    rowY += 3.8 * Mm;
                }

                y += height;
                gfx.DrawLine(rule, Left, y - 1.5 * Mm, Right, y - 1.5 * Mm);
            }

            y += 2 * Mm;
        }

        private void DrawTotals()
        {
            var rows = new List<(string Label, string Value, bool Strong)>
            {
                ("Summe netto", GermanFormat.Money(calc.LineTotal, doc.Currency), false),
            };

            foreach (var tax in calc.Taxes.Where(t => t.CategoryCode == InvoiceCalculator.StandardRated))
            {
                rows.Add(($"zzgl. USt. {GermanFormat.Percent(tax.Rate)} auf {GermanFormat.Money(tax.TaxableAmount, doc.Currency)}",
                    GermanFormat.Money(tax.TaxAmount, doc.Currency), false));
            }

            rows.Add(("Gesamtbetrag", GermanFormat.Money(calc.GrandTotal, doc.Currency), true));

            var height = rows.Count * 5.5 * Mm + 4 * Mm;
            EnsureSpace(height, repeatTableHeader: false);

            var labelX = Left + 85 * Mm;
            foreach (var (label, value, strong) in rows)
            {
                if (strong)
                {
                    gfx.DrawLine(new XPen(Ink, 0.8), labelX, y - 0.8 * Mm, Right, y - 0.8 * Mm);
                    y += 1 * Mm;
                }

                var font = Font(strong ? 10.5 : 9, strong);
                Text(label, font, strong ? ink : muted, labelX, y);
                TextRight(value, font, ink, ColAmountRight - 1.5 * Mm, y);
                y += 5.5 * Mm;
            }

            y += 4 * Mm;
        }

        private void DrawClosingText()
        {
            var paragraphs = new List<string>();

            foreach (var reason in calc.Taxes.Where(t => t.CategoryCode == InvoiceCalculator.Exempt && t.ExemptionReason is not null)
                         .Select(t => t.ExemptionReason!).Distinct())
            {
                paragraphs.Add(reason);
            }

            if (!string.IsNullOrWhiteSpace(doc.PaymentTerms))
            {
                paragraphs.Add(doc.PaymentTerms!);
            }

            var amount = GermanFormat.Money(calc.DuePayable, doc.Currency);
            if (!string.IsNullOrWhiteSpace(doc.Seller.Iban))
            {
                var until = doc.DueDate is { } due ? $" bis zum {GermanFormat.Date(due)}" : "";
                paragraphs.Add($"Bitte überweisen Sie den Betrag von {amount}{until} unter Angabe der Rechnungsnummer {doc.Number}.");
            }

            var font = Font(9);
            foreach (var paragraph in paragraphs)
            {
                foreach (var line in Wrap(paragraph, font, Width))
                {
                    EnsureSpace(5 * Mm, repeatTableHeader: false);
                    Text(line, font, ink, Left, y);
                    y += 4.4 * Mm;
                }

                y += 2 * Mm;
            }

            if (!string.IsNullOrWhiteSpace(doc.Seller.Iban))
            {
                var bank = new List<(string, string)>
                {
                    ("Kontoinhaber", doc.Seller.Name),
                    ("IBAN", GermanFormat.Iban(doc.Seller.Iban)),
                };
                if (!string.IsNullOrWhiteSpace(doc.Seller.Bic))
                {
                    bank.Add(("BIC", doc.Seller.Bic!));
                }

                if (!string.IsNullOrWhiteSpace(doc.Seller.BankName))
                {
                    bank.Add(("Bank", doc.Seller.BankName!));
                }

                EnsureSpace(bank.Count * 4.4 * Mm + 2 * Mm, repeatTableHeader: false);
                foreach (var (label, value) in bank)
                {
                    Text(label, font, muted, Left, y);
                    Text(Fit(value, font, Width - 30 * Mm), font, ink, Left + 30 * Mm, y);
                    y += 4.4 * Mm;
                }
            }
        }

        private void DrawFooters()
        {
            var font = Font(7);
            var seller = doc.Seller;
            var columns = new[]
            {
                new[] { seller.Name, seller.Street, $"{seller.PostalCode} {seller.City}" },
                new[] { $"Kontakt: {seller.ContactName}", $"Telefon: {seller.Phone}", $"E-Mail: {seller.Email}" },
                new[]
                {
                    string.IsNullOrWhiteSpace(seller.Iban) ? "" : $"IBAN: {GermanFormat.Iban(seller.Iban)}",
                    string.IsNullOrWhiteSpace(seller.VatId) ? "" : $"USt-IdNr.: {seller.VatId}",
                    string.IsNullOrWhiteSpace(seller.TaxNumber) ? "" : $"Steuernummer: {seller.TaxNumber}",
                },
            };
            var columnX = new[] { Left, Left + 55 * Mm, Left + 110 * Mm };

            for (var index = 0; index < pages.Count; index++)
            {
                using var footer = XGraphics.FromPdfPage(pages[index], XGraphicsPdfPageOptions.Append);
                footer.DrawLine(new XPen(Rule, 0.5), Left, 272 * Mm, Right, 272 * Mm);
                for (var column = 0; column < columns.Length; column++)
                {
                    var lineY = 274.5 * Mm;
                    foreach (var line in columns[column].Where(l => !string.IsNullOrWhiteSpace(l)))
                    {
                        footer.DrawString(Fit(line, font, 54 * Mm, footer), font, muted, columnX[column], lineY, XStringFormats.TopLeft);
                        lineY += 3.2 * Mm;
                    }
                }

                var pageLabel = $"Seite {index + 1} von {pages.Count}";
                var width = footer.MeasureString(pageLabel, font).Width;
                footer.DrawString(pageLabel, font, muted, Right - width, 286 * Mm, XStringFormats.TopLeft);
            }
        }

        private void Text(string text, XFont font, XBrush brush, double x, double top) =>
            gfx.DrawString(text, font, brush, x, top, XStringFormats.TopLeft);

        private void TextRight(string text, XFont font, XBrush brush, double right, double top) =>
            gfx.DrawString(text, font, brush, right - gfx.MeasureString(text, font).Width, top, XStringFormats.TopLeft);

        private string Fit(string text, XFont font, double maxWidth, XGraphics? graphics = null)
        {
            var g = graphics ?? gfx;
            if (g.MeasureString(text, font).Width <= maxWidth)
            {
                return text;
            }

            var shortened = text;
            while (shortened.Length > 1 && g.MeasureString(shortened + "…", font).Width > maxWidth)
            {
                shortened = shortened[..^1];
            }

            return shortened.TrimEnd() + "…";
        }

        private IEnumerable<string> Wrap(string text, XFont font, double maxWidth)
        {
            foreach (var paragraph in text.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
            {
                var line = "";
                foreach (var rawWord in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var word = rawWord;
                    var candidate = line.Length == 0 ? word : line + " " + word;
                    if (gfx.MeasureString(candidate, font).Width <= maxWidth)
                    {
                        line = candidate;
                        continue;
                    }

                    if (line.Length > 0)
                    {
                        yield return line;
                    }

                    // Überlange Wörter (z. B. Artikelnummern) hart umbrechen.
                    while (word.Length > 1 && gfx.MeasureString(word, font).Width > maxWidth)
                    {
                        var cut = word.Length - 1;
                        while (cut > 1 && gfx.MeasureString(word[..cut], font).Width > maxWidth)
                        {
                            cut--;
                        }

                        yield return word[..cut];
                        word = word[cut..];
                    }

                    line = word;
                }

                yield return line;
            }
        }

        private static string Join(string separator, params string?[] parts) =>
            string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
    }
}
