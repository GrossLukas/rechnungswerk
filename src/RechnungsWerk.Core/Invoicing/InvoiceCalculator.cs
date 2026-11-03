namespace RechnungsWerk.Core.Invoicing;

public sealed record CalculatedLine(int Position, InvoiceLineItem Item, string TaxCategoryCode, decimal VatRate, decimal NetAmount);

public sealed record TaxBreakdownEntry(string CategoryCode, decimal Rate, decimal TaxableAmount, decimal TaxAmount, string? ExemptionReason);

public sealed record InvoiceCalculation(
    IReadOnlyList<CalculatedLine> Lines,
    IReadOnlyList<TaxBreakdownEntry> Taxes,
    decimal LineTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    decimal DuePayable);

/// <summary>
/// Rechnet Positionen, Steuern je Satz und Summen nach EN 16931:
/// Positionsbetrag = Menge × Preis (kaufmännisch auf Cent gerundet), Steuer je Satz auf die Summe der Positionen.
/// </summary>
public static class InvoiceCalculator
{
    public const string StandardRated = "S";
    public const string Exempt = "E";

    public const string SmallBusinessExemptionReason = "Gemäß § 19 UStG wird keine Umsatzsteuer berechnet (Kleinunternehmerregelung).";

    public static InvoiceCalculation Calculate(InvoiceDocument document) =>
        Calculate(document.Lines, document.Seller.IsSmallBusiness, document.TaxExemptionReason);

    public static InvoiceCalculation Calculate(IEnumerable<InvoiceLineItem> items, bool isSmallBusiness, string? exemptionReason)
    {
        var lines = items
            .Select((item, index) =>
            {
                var exempt = isSmallBusiness || item.VatRate <= 0m;
                return new CalculatedLine(
                    index + 1,
                    item,
                    exempt ? Exempt : StandardRated,
                    exempt ? 0m : item.VatRate,
                    Money.Round(item.Quantity * item.UnitPrice));
            })
            .ToList();

        var taxes = lines
            .GroupBy(l => (l.TaxCategoryCode, l.VatRate))
            .OrderByDescending(g => g.Key.VatRate)
            .Select(g =>
            {
                var taxable = g.Sum(l => l.NetAmount);
                var isExempt = g.Key.TaxCategoryCode == Exempt;
                return new TaxBreakdownEntry(
                    g.Key.TaxCategoryCode,
                    g.Key.VatRate,
                    taxable,
                    isExempt ? 0m : Money.Round(taxable * g.Key.VatRate / 100m),
                    isExempt ? ExemptionReasonFor(isSmallBusiness, exemptionReason) : null);
            })
            .ToList();

        var lineTotal = lines.Sum(l => l.NetAmount);
        var taxTotal = taxes.Sum(t => t.TaxAmount);
        var grandTotal = lineTotal + taxTotal;
        return new InvoiceCalculation(lines, taxes, lineTotal, taxTotal, grandTotal, grandTotal);
    }

    private static string? ExemptionReasonFor(bool isSmallBusiness, string? exemptionReason) =>
        isSmallBusiness
            ? SmallBusinessExemptionReason
            : string.IsNullOrWhiteSpace(exemptionReason) ? null : exemptionReason.Trim();
}

public static class Money
{
    /// <summary>Kaufmännische Rundung auf zwei Nachkommastellen (0,005 → 0,01).</summary>
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
