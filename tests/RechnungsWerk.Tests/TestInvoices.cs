using RechnungsWerk.Core.Invoicing;

namespace RechnungsWerk.Tests;

/// <summary>Beispielrechnungen für Tests: eine an eine Behörde mit drei Steuersätzen, eine von einem Kleinunternehmer.</summary>
internal static class TestInvoices
{
    public static SellerInfo Seller(bool smallBusiness = false) => new()
    {
        Name = "Muster Software GmbH",
        Street = "Musterstraße 1",
        PostalCode = "10115",
        City = "Berlin",
        VatId = smallBusiness ? null : "DE123456789",
        TaxNumber = "12/345/67890",
        ContactName = "Erika Mustermann",
        Phone = "+49 30 1234567",
        Email = "rechnung@muster.example",
        Iban = "DE02 1203 0000 0000 2020 51",
        Bic = "BYLADEM1001",
        BankName = "Deutsche Kreditbank",
        IsSmallBusiness = smallBusiness,
    };

    public static InvoiceDocument Sample(bool smallBusiness = false) => new()
    {
        Number = "RE-2026-0042",
        IssueDate = new DateOnly(2026, 11, 3),
        DeliveryDate = new DateOnly(2026, 10, 31),
        DueDate = new DateOnly(2026, 11, 17),
        Note = "Vielen Dank für Ihren Auftrag.",
        TaxExemptionReason = "Steuerfrei nach § 4 Nr. 21 UStG",
        Seller = Seller(smallBusiness),
        Buyer = new BuyerInfo
        {
            Name = "Stadt Musterhausen",
            Street = "Rathausplatz 1",
            PostalCode = "20095",
            City = "Hamburg",
            ContactName = "Amt für Digitales",
            Email = "einkauf@musterhausen.example",
            LeitwegId = "04011000-1234512345-06",
            CustomerNumber = "K-0007",
        },
        Lines =
        [
            new InvoiceLineItem { Name = "Beratung Digitalisierung", Description = "Workshop vor Ort", Quantity = 7.5m, UnitCode = "HUR", UnitPrice = 95m, VatRate = 19m },
            new InvoiceLineItem { Name = "Fachbuch E-Rechnung", Quantity = 3m, UnitCode = "C62", UnitPrice = 0.335m, VatRate = 7m },
            new InvoiceLineItem { Name = "Schulung Mitarbeitende", Quantity = 1m, UnitCode = "LS", UnitPrice = 250m, VatRate = 0m },
        ],
    };

    /// <summary>Dieselbe Rechnung mit Leistungszeitraum statt Leistungsdatum – ohne Lieferdatum und ohne Lieferanschrift.</summary>
    public static InvoiceDocument WithServicePeriod(bool smallBusiness = false) => Sample(smallBusiness) with
    {
        DeliveryDate = null,
        ServicePeriodStart = new DateOnly(2026, 10, 1),
        ServicePeriodEnd = new DateOnly(2026, 10, 31),
    };
}
