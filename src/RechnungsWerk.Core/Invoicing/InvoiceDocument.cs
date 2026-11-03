namespace RechnungsWerk.Core.Invoicing;

/// <summary>Rechnungssteller, so wie er auf der Rechnung erscheint.</summary>
public sealed record SellerInfo
{
    public required string Name { get; init; }
    public required string Street { get; init; }
    public required string PostalCode { get; init; }
    public required string City { get; init; }
    public string CountryCode { get; init; } = "DE";
    public string? VatId { get; init; }
    public string? TaxNumber { get; init; }
    public required string ContactName { get; init; }
    public required string Phone { get; init; }
    public required string Email { get; init; }
    public string? Iban { get; init; }
    public string? Bic { get; init; }
    public string? BankName { get; init; }

    /// <summary>Kleinunternehmer nach § 19 UStG: keine Umsatzsteuer, Steuerkategorie E.</summary>
    public bool IsSmallBusiness { get; init; }
}

/// <summary>Rechnungsempfänger.</summary>
public sealed record BuyerInfo
{
    public required string Name { get; init; }
    public required string Street { get; init; }
    public required string PostalCode { get; init; }
    public required string City { get; init; }
    public string CountryCode { get; init; } = "DE";
    public string? ContactName { get; init; }
    public string? Email { get; init; }
    public string? LeitwegId { get; init; }
    public string? VatId { get; init; }
    public string? CustomerNumber { get; init; }
}

/// <summary>Eine Rechnungsposition mit Nettopreis.</summary>
public sealed record InvoiceLineItem
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public decimal Quantity { get; init; }

    /// <summary>Einheit nach UN/ECE Recommendation 20, z. B. C62 (Stück) oder HUR (Stunde).</summary>
    public string UnitCode { get; init; } = "C62";

    public decimal UnitPrice { get; init; }

    /// <summary>Umsatzsteuersatz in Prozent (19, 7 oder 0).</summary>
    public decimal VatRate { get; init; }
}

/// <summary>Alle Angaben, aus denen XRechnung, ZUGFeRD und das sichtbare PDF entstehen.</summary>
public sealed record InvoiceDocument
{
    public required string Number { get; init; }
    public required DateOnly IssueDate { get; init; }
    public DateOnly? DeliveryDate { get; init; }
    public DateOnly? ServicePeriodStart { get; init; }
    public DateOnly? ServicePeriodEnd { get; init; }
    public DateOnly? DueDate { get; init; }
    public string? PaymentTerms { get; init; }

    /// <summary>Käuferreferenz (BT-10), bei Behörden die Leitweg-ID.</summary>
    public string? BuyerReference { get; init; }

    public string? OrderReference { get; init; }
    public string? Note { get; init; }

    /// <summary>Grund für steuerfreie Positionen (0 %), z. B. „Steuerfrei nach § 4 Nr. 21 UStG“.</summary>
    public string? TaxExemptionReason { get; init; }

    public string Currency { get; init; } = "EUR";
    public required SellerInfo Seller { get; init; }
    public required BuyerInfo Buyer { get; init; }
    public required IReadOnlyList<InvoiceLineItem> Lines { get; init; }

    /// <summary>
    /// Käuferreferenz, wie sie in die E-Rechnung geschrieben wird. XRechnung verlangt sie immer (BR-DE-15):
    /// zuerst die Leitweg-ID, dann eine eigene Referenz, zuletzt die Kundennummer.
    /// </summary>
    public string EffectiveBuyerReference =>
        FirstFilled(Buyer.LeitwegId, BuyerReference, Buyer.CustomerNumber) ?? Number;

    private static string? FirstFilled(params string?[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrEmpty(v));
}
