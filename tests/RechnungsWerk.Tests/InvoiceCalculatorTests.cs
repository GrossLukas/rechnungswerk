using RechnungsWerk.Core.Invoicing;

namespace RechnungsWerk.Tests;

public sealed class InvoiceCalculatorTests
{
    [Theory]
    [InlineData("0.125", "0.13")]
    [InlineData("0.124", "0.12")]
    [InlineData("1.005", "1.01")]
    [InlineData("-0.125", "-0.13")]
    [InlineData("135.375", "135.38")]
    public void Money_rounds_commercially(string input, string expected)
    {
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            Money.Round(decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Calculates_lines_taxes_and_totals_per_rate()
    {
        var calculation = InvoiceCalculator.Calculate(TestInvoices.Sample());

        Assert.Equal([712.50m, 1.01m, 250.00m], calculation.Lines.Select(l => l.NetAmount));
        Assert.Collection(calculation.Taxes,
            t => Assert.Equal(("S", 19m, 712.50m, 135.38m), (t.CategoryCode, t.Rate, t.TaxableAmount, t.TaxAmount)),
            t => Assert.Equal(("S", 7m, 1.01m, 0.07m), (t.CategoryCode, t.Rate, t.TaxableAmount, t.TaxAmount)),
            t =>
            {
                Assert.Equal(("E", 0m, 250.00m, 0m), (t.CategoryCode, t.Rate, t.TaxableAmount, t.TaxAmount));
                Assert.Equal("Steuerfrei nach § 4 Nr. 21 UStG", t.ExemptionReason);
            });
        Assert.Equal(963.51m, calculation.LineTotal);
        Assert.Equal(135.45m, calculation.TaxTotal);
        Assert.Equal(1098.96m, calculation.GrandTotal);
        Assert.Equal(calculation.GrandTotal, calculation.DuePayable);
    }

    [Fact]
    public void Rounds_tax_on_the_sum_per_rate_not_per_line()
    {
        // Je Position gerundet wären es 3 × 0,20 € = 0,60 €; EN 16931 rechnet auf die Summe: 3,09 € × 19 % = 0,59 €.
        var items = Enumerable.Range(0, 3)
            .Select(_ => new InvoiceLineItem { Name = "Kleinteil", Quantity = 1m, UnitPrice = 1.03m, VatRate = 19m });

        var calculation = InvoiceCalculator.Calculate(items, isSmallBusiness: false, exemptionReason: null);

        Assert.Equal(0.59m, Assert.Single(calculation.Taxes).TaxAmount);
        Assert.Equal(3.68m, calculation.GrandTotal);
    }

    [Fact]
    public void Small_business_uses_exempt_category_with_reason_and_no_tax()
    {
        var calculation = InvoiceCalculator.Calculate(TestInvoices.Sample(smallBusiness: true));

        Assert.All(calculation.Lines, l => Assert.Equal(("E", 0m), (l.TaxCategoryCode, l.VatRate)));
        var tax = Assert.Single(calculation.Taxes);
        Assert.Equal(InvoiceCalculator.SmallBusinessExemptionReason, tax.ExemptionReason);
        Assert.Equal(0m, calculation.TaxTotal);
        Assert.Equal(963.51m, calculation.GrandTotal);
    }

    [Fact]
    public void Negative_quantities_reduce_the_total()
    {
        var items = new[]
        {
            new InvoiceLineItem { Name = "Leistung", Quantity = 2m, UnitPrice = 50m, VatRate = 19m },
            new InvoiceLineItem { Name = "Gutschrift Stammkunde", Quantity = -1m, UnitPrice = 10m, VatRate = 19m },
        };

        var calculation = InvoiceCalculator.Calculate(items, isSmallBusiness: false, exemptionReason: null);

        Assert.Equal(90m, calculation.LineTotal);
        Assert.Equal(17.10m, calculation.TaxTotal);
    }

    [Theory]
    [InlineData("DE02 1203 0000 0000 2020 51", true)]
    [InlineData("de02120300000000202051", true)]
    [InlineData("DE02120300000000202052", false)]
    [InlineData("DE0212030000000020205", false)]
    [InlineData("AT611904300234573201", true)]
    [InlineData("keine IBAN", false)]
    public void Validates_iban_checksum(string iban, bool valid) => Assert.Equal(valid, Iban.IsValid(iban));
}
