namespace RechnungsWerk.Core.EInvoice;

/// <summary>Lesbare Darstellung einer empfangenen oder erzeugten E-Rechnung, unabhängig von der Syntax.</summary>
public sealed class InvoiceView
{
    public required string FormatLabel { get; init; }
    public required string Syntax { get; init; }
    public string? SpecificationId { get; init; }
    public bool FromPdf { get; init; }

    public string? Number { get; init; }
    public string? TypeCode { get; init; }
    public string TypeLabel { get; init; } = "Rechnung";
    public DateOnly? IssueDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public DateOnly? DeliveryDate { get; init; }
    public DateOnly? PeriodStart { get; init; }
    public DateOnly? PeriodEnd { get; init; }
    public string Currency { get; init; } = "EUR";
    public string? BuyerReference { get; init; }
    public bool BuyerReferenceIsLeitwegId { get; init; }
    public string? OrderReference { get; init; }
    public string? ContractReference { get; init; }

    public required PartyView Seller { get; init; }
    public required PartyView Buyer { get; init; }

    public IReadOnlyList<LineView> Lines { get; init; } = [];
    public IReadOnlyList<AllowanceChargeView> AllowanceCharges { get; init; } = [];
    public IReadOnlyList<TaxView> Taxes { get; init; } = [];
    public required TotalsView Totals { get; init; }
    public required PaymentView Payment { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
    public IReadOnlyList<AttachmentView> Attachments { get; init; } = [];
}

public sealed class PartyView
{
    public string? Name { get; init; }
    public string? Street { get; init; }
    public string? AdditionalStreet { get; init; }
    public string? PostalCode { get; init; }
    public string? City { get; init; }
    public string? CountryCode { get; init; }
    public string? Identifier { get; init; }
    public IReadOnlyList<string> VatIds { get; init; } = [];
    public IReadOnlyList<string> TaxNumbers { get; init; } = [];
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public string? ContactEmail { get; init; }
    public string? ElectronicAddress { get; init; }
    public string? ElectronicAddressLabel { get; init; }
}

public sealed record LineView(
    string? Id,
    string Name,
    string? Description,
    decimal Quantity,
    string? UnitCode,
    decimal UnitPrice,
    decimal? NetAmount,
    decimal VatRate,
    string? TaxCategoryCode,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd);

public sealed record AllowanceChargeView(bool IsCharge, string? Reason, decimal Amount, string? TaxCategoryCode, decimal? VatRate);

public sealed record TaxView(string? CategoryCode, decimal Rate, decimal TaxableAmount, decimal TaxAmount, string? ExemptionReason);

public sealed record TotalsView(
    decimal? LineTotal,
    decimal? AllowanceTotal,
    decimal? ChargeTotal,
    decimal? TaxBasis,
    decimal? TaxTotal,
    decimal? GrandTotal,
    decimal? Prepaid,
    decimal? Rounding,
    decimal? DuePayable);

public sealed class PaymentView
{
    public string? MeansCode { get; init; }
    public string? MeansLabel { get; init; }
    public string? Information { get; init; }
    public string? PaymentReference { get; init; }
    public IReadOnlyList<BankAccountView> Accounts { get; init; } = [];
    public IReadOnlyList<string> Terms { get; init; } = [];
    public string? CreditorId { get; init; }
    public string? MandateReference { get; init; }
    public string? DebitedIban { get; init; }
}

public sealed record BankAccountView(string? Iban, string? Bic, string? BankName, string? AccountName);

/// <summary>
/// Anhang aus der Rechnung (BG-24) oder zusätzlich im PDF eingebettete Datei. Die Ansicht behält nur die Größe –
/// der Inhalt bliebe sonst für die ganze Sitzung im Speicher, obwohl ihn niemand anzeigt.
/// </summary>
public sealed record AttachmentView(
    string? Id,
    string? Description,
    string? FileName,
    string? MimeType,
    long? Size,
    string? ExternalUri,
    bool EmbeddedInPdf);
