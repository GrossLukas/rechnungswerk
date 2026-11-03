using System.Text.RegularExpressions;
using System.Xml.Linq;
using RechnungsWerk.Core.Invoicing;
using s2industries.ZUGFeRD;

namespace RechnungsWerk.Core.EInvoice;

/// <summary>Ergebnis des Einlesens: das reine Rechnungs-XML und die lesbare Ansicht.</summary>
public sealed record EInvoiceReadResult(byte[] Xml, InvoiceView View);

/// <summary>
/// Liest XRechnung (UBL oder CII) und ZUGFeRD/Factur-X (PDF mit eingebettetem XML). Alles passiert im Speicher.
/// </summary>
public sealed partial class EInvoiceReader
{
    public const int MaxFileBytes = 10 * 1024 * 1024;

    private static readonly XNamespace UblInvoice = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace UblCreditNote = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Cii = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";
    private static readonly XNamespace Ram = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";
    private static readonly XNamespace Rsm = Cii;
    private static readonly XNamespace Zugferd1 = "urn:ferd:CrossIndustryDocument:invoice:1p0";

    /// <summary>Gibt nur das Rechnungs-XML zurück (für die Prüfung durch den Validator).</summary>
    public byte[] ExtractXml(byte[] content) => Prepare(content).Xml;

    public EInvoiceReadResult Read(byte[] content)
    {
        var (xml, fromPdf, otherFiles, syntax) = Prepare(content);

        InvoiceDescriptor descriptor;
        try
        {
            descriptor = InvoiceDescriptor.Load(new MemoryStream(xml, writable: false));
        }
        catch (Exception ex)
        {
            throw new EInvoiceReadException(
                $"Die {syntax.Label}-Datei konnte nicht vollständig gelesen werden. Möglicherweise fehlen Pflichtangaben oder die Struktur ist fehlerhaft. " +
                "Lass die Datei über „Prüfen“ offiziell validieren, um die genauen Fehler zu sehen.", ex);
        }

        if (descriptor is null)
        {
            throw new EInvoiceReadException("Die Datei enthält keine lesbare Rechnung.");
        }

        var view = Map(descriptor, syntax, fromPdf, otherFiles);
        return new EInvoiceReadResult(xml, view);
    }

    private static (byte[] Xml, bool FromPdf, IReadOnlyList<PdfXmlExtractor.EmbeddedFile> OtherFiles, SyntaxInfo Syntax) Prepare(byte[] content)
    {
        if (content.Length == 0)
        {
            throw new EInvoiceReadException("Die Datei ist leer.");
        }

        if (content.Length > MaxFileBytes)
        {
            throw new EInvoiceReadException("Die Datei ist größer als 10 MB. So große E-Rechnungen werden nicht verarbeitet.");
        }

        var fromPdf = PdfXmlExtractor.IsPdf(content);
        byte[] xml;
        IReadOnlyList<PdfXmlExtractor.EmbeddedFile> otherFiles = [];
        if (fromPdf)
        {
            (xml, otherFiles) = PdfXmlExtractor.Extract(content);
        }
        else
        {
            xml = content;
        }

        var syntax = DetectSyntax(SecureXml.Load(xml));
        return (xml, fromPdf, otherFiles, syntax);
    }

    private sealed record SyntaxInfo(string Syntax, string Label, string? SpecificationId, string? TypeCode);

    private static SyntaxInfo DetectSyntax(XDocument document)
    {
        var root = document.Root ?? throw new EInvoiceReadException("Die Datei enthält kein XML-Wurzelelement.");

        if (root.Name == UblInvoice + "Invoice" || root.Name == UblCreditNote + "CreditNote")
        {
            var spec = root.Element(Cbc + "CustomizationID")?.Value.Trim();
            var type = (root.Element(Cbc + "InvoiceTypeCode") ?? root.Element(Cbc + "CreditNoteTypeCode"))?.Value.Trim();
            if (root.Name.LocalName == "CreditNote")
            {
                type ??= "381";
            }

            return new SyntaxInfo("UBL", $"{ProfileName(spec)} (UBL)", spec, type);
        }

        if (root.Name == Rsm + "CrossIndustryInvoice")
        {
            var spec = root.Element(Rsm + "ExchangedDocumentContext")?.Element(Ram + "GuidelineSpecifiedDocumentContextParameter")?.Element(Ram + "ID")?.Value.Trim();
            var type = root.Element(Rsm + "ExchangedDocument")?.Element(Ram + "TypeCode")?.Value.Trim();
            return new SyntaxInfo("CII", $"{ProfileName(spec)} (CII)", spec, type);
        }

        if (root.Name.Namespace == Zugferd1)
        {
            return new SyntaxInfo("CII", "ZUGFeRD 1.0", null, null);
        }

        throw new EInvoiceReadException(
            "Unbekanntes Format. Erwartet wird eine XRechnung (UBL oder CII) als XML-Datei oder ein ZUGFeRD-/Factur-X-PDF. " +
            $"Gefunden wurde ein XML-Dokument mit dem Wurzelelement „{root.Name.LocalName}“.");
    }

    private static string ProfileName(string? specificationId)
    {
        var spec = specificationId ?? "";
        if (spec.Contains("xrechnung", StringComparison.OrdinalIgnoreCase))
        {
            var version = XRechnungVersion().Match(spec);
            var name = version.Success ? $"XRechnung {version.Groups[1].Value.Replace('_', '.')}" : "XRechnung";
            return spec.Contains("extension", StringComparison.OrdinalIgnoreCase) ? name + " Extension" : name;
        }

        if (spec.Contains("factur-x.eu:1p0:extended", StringComparison.OrdinalIgnoreCase) || spec.Contains(":extended", StringComparison.OrdinalIgnoreCase))
        {
            return "ZUGFeRD/Factur-X Extended";
        }

        if (spec.Contains("factur-x.eu:1p0:basicwl", StringComparison.OrdinalIgnoreCase))
        {
            return "ZUGFeRD/Factur-X Basic WL";
        }

        if (spec.Contains("factur-x.eu:1p0:basic", StringComparison.OrdinalIgnoreCase))
        {
            return "ZUGFeRD/Factur-X Basic";
        }

        if (spec.Contains("factur-x.eu:1p0:minimum", StringComparison.OrdinalIgnoreCase))
        {
            return "ZUGFeRD/Factur-X Minimum";
        }

        if (spec.Contains("peppol", StringComparison.OrdinalIgnoreCase))
        {
            return "Peppol BIS Billing";
        }

        return spec.StartsWith("urn:cen.eu:en16931:2017", StringComparison.OrdinalIgnoreCase) ? "EN 16931" : "E-Rechnung";
    }

    private static InvoiceView Map(InvoiceDescriptor d, SyntaxInfo syntax, bool fromPdf,
        IReadOnlyList<PdfXmlExtractor.EmbeddedFile> otherFiles)
    {
        var buyerReference = Clean(d.ReferenceOrderNo);
        var attachments = (d.AdditionalReferencedDocuments ?? [])
            .Where(a => a.AttachmentBinaryObject is { Length: > 0 } || !string.IsNullOrWhiteSpace(a.URIID) || !string.IsNullOrWhiteSpace(a.Filename))
            .Select(a => new AttachmentView(Clean(a.ID), Clean(a.Name), Clean(a.Filename), Clean(a.MimeType), a.AttachmentBinaryObject,
                Clean(a.URIID), EmbeddedInPdf: false))
            .Concat(otherFiles.Select(f => new AttachmentView(null, f.Description, f.Name, f.MimeType, f.Content, null, EmbeddedInPdf: true)))
            .ToList();

        var taxes = (d.Taxes ?? [])
            .Select(t => new TaxView(t.CategoryCode?.ToString(), t.Percent, t.BasisAmount, t.TaxAmount, Clean(t.ExemptionReason)))
            .ToList();

        var lines = (d.TradeLineItems ?? [])
            .Select(l => new LineView(
                Clean(l.AssociatedDocument?.LineID),
                Clean(l.Name) ?? Clean(l.AssociatedDocument?.Notes?.FirstOrDefault()?.Content) ?? "Position",
                Clean(l.Description),
                l.BilledQuantity,
                l.UnitCode?.ToString(),
                l.NetUnitPrice,
                l.LineTotalAmount,
                l.TaxPercent,
                l.TaxCategoryCode?.ToString(),
                ToDate(l.BillingPeriodStart),
                ToDate(l.BillingPeriodEnd)))
            .ToList();

        var allowanceCharges = (d.TradeAllowanceCharges ?? [])
            .Select(c => new AllowanceChargeView(c.ChargeIndicator, Clean(c.Reason), c.ActualAmount, c.Tax?.CategoryCode?.ToString(), c.Tax?.Percent))
            .ToList();

        var dueDate = (d.PaymentTerms ?? []).Select(t => ToDate(t.DueDate)).FirstOrDefault(t => t is not null);
        var meansCode = d.PaymentMeans?.TypeCode is { } code ? ((int)code).ToString(System.Globalization.CultureInfo.InvariantCulture) : null;

        return new InvoiceView
        {
            FormatLabel = fromPdf ? $"{syntax.Label} im PDF" : syntax.Label,
            Syntax = syntax.Syntax,
            SpecificationId = syntax.SpecificationId,
            FromPdf = fromPdf,
            Number = Clean(d.InvoiceNo),
            TypeCode = syntax.TypeCode,
            TypeLabel = CodeLabels.InvoiceType(syntax.TypeCode),
            IssueDate = ToDate(d.InvoiceDate),
            DueDate = dueDate,
            DeliveryDate = ToDate(d.ActualDeliveryDate),
            PeriodStart = ToDate(d.BillingPeriodStart),
            PeriodEnd = ToDate(d.BillingPeriodEnd),
            Currency = d.Currency.ToString(),
            BuyerReference = buyerReference,
            BuyerReferenceIsLeitwegId = buyerReference is not null && LeitwegIdPattern().IsMatch(buyerReference),
            OrderReference = Clean(d.OrderNo),
            ContractReference = Clean(d.ContractReferencedDocument?.ID),
            Seller = MapParty(d.Seller, d.SellerContact, d.SellerTaxRegistration, d.SellerElectronicAddress),
            Buyer = MapParty(d.Buyer, d.BuyerContact, d.BuyerTaxRegistration, d.BuyerElectronicAddress),
            Lines = lines,
            AllowanceCharges = allowanceCharges,
            Taxes = taxes,
            Totals = new TotalsView(d.LineTotalAmount, d.AllowanceTotalAmount, d.ChargeTotalAmount, d.TaxBasisAmount, d.TaxTotalAmount,
                d.GrandTotalAmount, d.TotalPrepaidAmount, d.RoundingAmount, d.DuePayableAmount),
            Payment = new PaymentView
            {
                MeansCode = meansCode,
                MeansLabel = CodeLabels.PaymentMeans(meansCode),
                Information = Clean(d.PaymentMeans?.Information),
                PaymentReference = Clean(d.PaymentReference),
                Accounts = (d.CreditorBankAccounts ?? [])
                    .Select(a => new BankAccountView(Clean(a.IBAN), Clean(a.BIC), Clean(a.BankName), Clean(a.Name)))
                    .ToList(),
                Terms = (d.PaymentTerms ?? []).Select(t => Clean(t.Description)).OfType<string>().ToList(),
                CreditorId = Clean(d.PaymentMeans?.SEPACreditorIdentifier),
                MandateReference = Clean(d.PaymentMeans?.SEPAMandateReference),
                DebitedIban = Clean(d.DebitorBankAccounts?.FirstOrDefault()?.IBAN),
            },
            Notes = (d.Notes ?? []).Select(n => Clean(n.Content)).OfType<string>().ToList(),
            Attachments = attachments,
        };
    }

    private static PartyView MapParty(Party? party, Contact? contact, List<TaxRegistration>? registrations, ElectronicAddress? address) => new()
    {
        Name = Clean(party?.Name),
        Street = Clean(party?.Street),
        AdditionalStreet = Clean(party?.AddressLine3),
        PostalCode = Clean(party?.Postcode),
        City = Clean(party?.City),
        CountryCode = party?.Country?.ToString(),
        Identifier = Clean(party?.ID?.ID),
        VatIds = (registrations ?? []).Where(r => r.SchemeID == TaxRegistrationSchemeID.VA).Select(r => Clean(r.No)).OfType<string>().ToList(),
        TaxNumbers = (registrations ?? []).Where(r => r.SchemeID == TaxRegistrationSchemeID.FC).Select(r => Clean(r.No)).OfType<string>().ToList(),
        ContactName = Clean(contact?.Name) ?? Clean(party?.ContactName),
        ContactPhone = Clean(contact?.PhoneNo),
        ContactEmail = Clean(contact?.EmailAddress),
        ElectronicAddress = Clean(address?.Address),
        ElectronicAddressLabel = address is null ? null : address.ElectronicAddressSchemeID switch
        {
            ElectronicAddressSchemeIdentifiers.ElectronicMailSmtp => "E-Mail",
            ElectronicAddressSchemeIdentifiers.LeitwegId => "Leitweg-ID",
            ElectronicAddressSchemeIdentifiers.GermanyVatNumber => "USt-IdNr.",
            _ => "Elektronische Adresse",
        },
    };

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Mehrzeilige Texte behalten ihre Zeilen, aber ohne Einrückung aus dem XML.
        var lines = value.Replace("\r", "", StringComparison.Ordinal).Split('\n').Select(l => l.Trim());
        return string.Join('\n', lines).Trim();
    }

    private static DateOnly? ToDate(DateTime? value) => value is { } date ? DateOnly.FromDateTime(date) : null;

    [GeneratedRegex(@"xrechnung_(\d+(?:[._]\d+)*)", RegexOptions.IgnoreCase)]
    private static partial Regex XRechnungVersion();

    // Leitweg-ID: Grobadressierung (2–12 Ziffern), optional Feinadressierung, zweistellige Prüfziffer.
    [GeneratedRegex(@"^\d{2,12}(-[A-Za-z0-9]{1,30})?-\d{2}$")]
    private static partial Regex LeitwegIdPattern();
}
