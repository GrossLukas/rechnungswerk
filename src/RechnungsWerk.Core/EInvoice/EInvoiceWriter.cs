using RechnungsWerk.Core.Invoicing;
using RechnungsWerk.Core.Pdf;
using s2industries.ZUGFeRD;
using s2industries.ZUGFeRD.PDF;

namespace RechnungsWerk.Core.EInvoice;

/// <summary>
/// Schreibt eine <see cref="InvoiceDocument"/> als XRechnung (UBL/CII) oder als ZUGFeRD-PDF.
/// Die Beträge kommen ausschließlich aus dem <see cref="InvoiceCalculator"/>, damit Ansicht, PDF und XML übereinstimmen.
/// </summary>
public sealed class EInvoiceWriter(InvoicePdfRenderer pdfRenderer)
{
    public const string BusinessProcessBilling = "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0";

    public EInvoiceWriter() : this(new InvoicePdfRenderer())
    {
    }

    public byte[] Write(InvoiceDocument document, EInvoiceFormat format)
    {
        var calculation = InvoiceCalculator.Calculate(document);
        var descriptor = BuildDescriptor(document, calculation);

        using var output = new MemoryStream();
        switch (format)
        {
            case EInvoiceFormat.XRechnungUbl:
                descriptor.Save(output, ZUGFeRDVersion.Version23, Profile.XRechnung, ZUGFeRDFormats.UBL);
                return UblPaymentTermsWorkaround.Apply(output.ToArray());
            case EInvoiceFormat.XRechnungCii:
                descriptor.Save(output, ZUGFeRDVersion.Version23, Profile.XRechnung, ZUGFeRDFormats.CII);
                return CiiDeliveryWorkaround.Apply(output.ToArray());
            case EInvoiceFormat.ZugferdPdf:
                var visiblePdf = pdfRenderer.Render(document, calculation);
                using (var source = new MemoryStream(visiblePdf, writable: false))
                {
                    // Profil „Comfort“ entspricht EN 16931 (ZUGFeRD 2.3 / Factur-X 1.0).
                    InvoicePdfProcessor.SaveToPdf(output, ZUGFeRDVersion.Version23, Profile.Comfort, ZUGFeRDFormats.CII, source, descriptor);
                }

                return CiiDeliveryWorkaround.ApplyToPdf(output.ToArray());
            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    internal static InvoiceDescriptor BuildDescriptor(InvoiceDocument doc, InvoiceCalculation calc)
    {
        var descriptor = InvoiceDescriptor.CreateInvoice(doc.Number, ToDateTime(doc.IssueDate), Currency(doc.Currency));
        descriptor.Type = InvoiceType.Invoice;

        // BT-23: XRechnung verlangt in CII den Geschäftsprozess (PEPPOL-EN16931-R001); für ZUGFeRD ist er unschädlich.
        descriptor.BusinessProcess = BusinessProcessBilling;

        var seller = doc.Seller;

        // BR-CO-26 verlangt Verkäuferkennung (BT-29), Registernummer (BT-30) oder USt-IdNr. (BT-31). Die Steuernummer (BT-32)
        // zählt dort nicht – ohne USt-IdNr. (typisch für Kleinunternehmer) dient sie deshalb zusätzlich als Verkäuferkennung.
        var sellerId = string.IsNullOrWhiteSpace(seller.VatId) && !string.IsNullOrWhiteSpace(seller.TaxNumber) ? seller.TaxNumber.Trim() : null;
        descriptor.SetSeller(seller.Name, seller.PostalCode, seller.City, seller.Street, Country(seller.CountryCode, "Rechnungsstellers"), id: sellerId);
        descriptor.SetSellerContact(name: seller.ContactName, emailAddress: seller.Email, phoneno: seller.Phone);
        descriptor.SetSellerElectronicAddress(seller.Email, ElectronicAddressSchemeIdentifiers.ElectronicMailSmtp);
        if (!string.IsNullOrWhiteSpace(seller.VatId))
        {
            descriptor.AddSellerTaxRegistration(Compact(seller.VatId), TaxRegistrationSchemeID.VA);
        }

        if (!string.IsNullOrWhiteSpace(seller.TaxNumber))
        {
            descriptor.AddSellerTaxRegistration(seller.TaxNumber.Trim(), TaxRegistrationSchemeID.FC);
        }

        var buyer = doc.Buyer;
        descriptor.SetBuyer(buyer.Name, buyer.PostalCode, buyer.City, buyer.Street, Country(buyer.CountryCode, "Rechnungsempfängers"),
            id: string.IsNullOrWhiteSpace(buyer.CustomerNumber) ? null : buyer.CustomerNumber);
        if (!string.IsNullOrWhiteSpace(buyer.ContactName))
        {
            descriptor.SetBuyerContact(buyer.ContactName);
        }

        if (!string.IsNullOrWhiteSpace(buyer.VatId))
        {
            descriptor.AddBuyerTaxRegistration(Compact(buyer.VatId), TaxRegistrationSchemeID.VA);
        }

        if (!string.IsNullOrWhiteSpace(buyer.LeitwegId))
        {
            descriptor.SetBuyerElectronicAddress(buyer.LeitwegId.Trim(), ElectronicAddressSchemeIdentifiers.LeitwegId);
        }
        else if (!string.IsNullOrWhiteSpace(buyer.Email))
        {
            descriptor.SetBuyerElectronicAddress(buyer.Email.Trim(), ElectronicAddressSchemeIdentifiers.ElectronicMailSmtp);
        }

        descriptor.ReferenceOrderNo = doc.EffectiveBuyerReference;
        if (!string.IsNullOrWhiteSpace(doc.OrderReference))
        {
            descriptor.SetBuyerOrderReferenceDocument(doc.OrderReference.Trim());
        }

        if (doc.ServicePeriodStart is { } start && doc.ServicePeriodEnd is { } end)
        {
            descriptor.SetBillingPeriod(ToDateTime(start), ToDateTime(end));
        }
        else if (doc.DeliveryDate is { } delivery)
        {
            descriptor.ActualDeliveryDate = ToDateTime(delivery);
        }

        if (!string.IsNullOrWhiteSpace(doc.Note))
        {
            descriptor.AddNote(doc.Note.Trim());
        }

        if (!string.IsNullOrWhiteSpace(seller.Iban))
        {
            descriptor.SetPaymentMeans(PaymentMeansTypeCodes.SEPACreditTransfer, "Überweisung");
            descriptor.AddCreditorFinancialAccount(Invoicing.Iban.Normalize(seller.Iban),
                string.IsNullOrWhiteSpace(seller.Bic) ? null : Compact(seller.Bic),
                bankName: string.IsNullOrWhiteSpace(seller.BankName) ? null : seller.BankName.Trim(),
                name: seller.Name);
            descriptor.PaymentReference = doc.Number;
        }
        else
        {
            descriptor.SetPaymentMeans(PaymentMeansTypeCodes.InstrumentNotDefined);
        }

        // Leere Zahlungsbedingungen wären ein leeres XML-Element; daher bei reinem Fälligkeitsdatum ein Standardtext.
        var terms = string.IsNullOrWhiteSpace(doc.PaymentTerms) ? null : doc.PaymentTerms.Trim();
        if (terms is null && doc.DueDate is { } dueDate)
        {
            terms = $"Zahlbar bis {Formatting.GermanFormat.Date(dueDate)} ohne Abzug.";
        }

        if (terms is not null)
        {
            descriptor.AddTradePaymentTerms(terms, doc.DueDate is { } due ? ToDateTime(due) : null);
        }

        foreach (var line in calc.Lines)
        {
            descriptor.AddTradeLineItem(
                lineID: line.Position.ToString(System.Globalization.CultureInfo.InvariantCulture),
                name: line.Item.Name,
                netUnitPrice: line.Item.UnitPrice,
                unitCode: Unit(line.Item.UnitCode),
                description: string.IsNullOrWhiteSpace(line.Item.Description) ? null : line.Item.Description.Trim(),
                billedQuantity: line.Item.Quantity,
                lineTotalAmount: line.NetAmount,
                taxType: TaxTypes.VAT,
                categoryCode: Category(line.TaxCategoryCode),
                taxPercent: line.VatRate);
        }

        foreach (var tax in calc.Taxes)
        {
            descriptor.AddApplicableTradeTax(tax.TaxableAmount, tax.Rate, tax.TaxAmount, TaxTypes.VAT, Category(tax.CategoryCode),
                exemptionReason: tax.ExemptionReason);
        }

        descriptor.SetTotals(
            lineTotalAmount: calc.LineTotal,
            taxBasisAmount: calc.LineTotal,
            taxTotalAmount: calc.TaxTotal,
            grandTotalAmount: calc.GrandTotal,
            duePayableAmount: calc.DuePayable);

        return descriptor;
    }

    private static DateTime ToDateTime(DateOnly date) => date.ToDateTime(TimeOnly.MinValue);

    private static string Compact(string value) => value.Replace(" ", "", StringComparison.Ordinal).Trim().ToUpperInvariant();

    /// <summary>Ob ein Ländercode in eine E-Rechnung geschrieben werden kann – dieselbe Liste, mit der geschrieben wird.</summary>
    public static bool IsKnownCountry(string? code) => TryCountry(code, out _);

    // Nur zwei Buchstaben: Enum.TryParse nähme sonst auch Zahlen („12“ wäre Australien).
    private static bool TryCountry(string? code, out CountryCodes country)
    {
        country = default;
        var trimmed = code?.Trim();
        return trimmed is { Length: 2 } && trimmed.All(char.IsAsciiLetter)
               && Enum.TryParse(trimmed, ignoreCase: true, out country) && Enum.IsDefined(country);
    }

    private static CountryCodes Country(string? code, string party) =>
        TryCountry(code, out var country)
            ? country
            : throw new EInvoiceWriteException($"Der Ländercode „{code}“ des {party} ist unbekannt. Gültig sind zweistellige Codes wie DE, AT oder GB.");

    private static CurrencyCodes Currency(string? code) =>
        Enum.TryParse<CurrencyCodes>(code?.Trim(), ignoreCase: true, out var currency) && Enum.IsDefined(currency)
            ? currency
            : throw new EInvoiceWriteException($"Die Währung „{code}“ ist unbekannt.");

    private static QuantityCodes Unit(string? code) =>
        Enum.TryParse<QuantityCodes>(code?.Trim(), ignoreCase: true, out var unit) && Enum.IsDefined(unit)
            ? unit
            : throw new EInvoiceWriteException($"Die Einheit „{code}“ ist unbekannt.");

    private static TaxCategoryCodes Category(string code) => code switch
    {
        InvoiceCalculator.StandardRated => TaxCategoryCodes.S,
        InvoiceCalculator.Exempt => TaxCategoryCodes.E,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unbekannte Steuerkategorie."),
    };
}

/// <summary>Die Rechnungsdaten lassen sich nicht als E-Rechnung schreiben; die deutsche Meldung darf direkt angezeigt werden.</summary>
public sealed class EInvoiceWriteException(string message) : Exception(message);
