using System.Text;
using RechnungsWerk.Core.EInvoice;
using RechnungsWerk.Core.Invoicing;

namespace RechnungsWerk.Tests;

public sealed class EInvoiceRoundTripTests
{
    private readonly EInvoiceWriter writer = new();
    private readonly EInvoiceReader reader = new();

    public static TheoryData<EInvoiceFormat, bool> AllFormats()
    {
        var data = new TheoryData<EInvoiceFormat, bool>();
        foreach (var format in Enum.GetValues<EInvoiceFormat>())
        {
            data.Add(format, false);
            data.Add(format, true);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllFormats))]
    public void Written_invoice_reads_back_with_identical_amounts(EInvoiceFormat format, bool smallBusiness)
    {
        var document = TestInvoices.Sample(smallBusiness);
        var expected = InvoiceCalculator.Calculate(document);

        var view = reader.Read(writer.Write(document, format)).View;

        Assert.Equal(document.Number, view.Number);
        Assert.Equal(document.IssueDate, view.IssueDate);
        Assert.Equal(document.DueDate, view.DueDate);
        Assert.Equal(document.DeliveryDate, view.DeliveryDate);
        Assert.Equal("EUR", view.Currency);
        Assert.Equal(format == EInvoiceFormat.ZugferdPdf, view.FromPdf);

        Assert.Equal(expected.LineTotal, view.Totals.LineTotal);
        Assert.Equal(expected.LineTotal, view.Totals.TaxBasis);
        Assert.Equal(expected.TaxTotal, view.Totals.TaxTotal);
        Assert.Equal(expected.GrandTotal, view.Totals.GrandTotal);
        Assert.Equal(expected.DuePayable, view.Totals.DuePayable);

        Assert.Equal(
            expected.Taxes.Select(t => ((string?)t.CategoryCode, t.Rate, t.TaxableAmount, t.TaxAmount, t.ExemptionReason)),
            view.Taxes.Select(t => (t.CategoryCode, t.Rate, t.TaxableAmount, t.TaxAmount, t.ExemptionReason)));

        Assert.Equal(
            expected.Lines.Select(l => (l.Item.Name, l.Item.Quantity, (string?)l.Item.UnitCode, l.Item.UnitPrice, (decimal?)l.NetAmount, l.VatRate)),
            view.Lines.Select(l => (l.Name, l.Quantity, l.UnitCode, l.UnitPrice, l.NetAmount, l.VatRate)));

        Assert.Equal(document.Seller.Name, view.Seller.Name);
        Assert.Equal(document.Buyer.Name, view.Buyer.Name);
        Assert.Equal("04011000-1234512345-06", view.BuyerReference);
        Assert.True(view.BuyerReferenceIsLeitwegId);
        Assert.Equal("DE02120300000000202051", Assert.Single(view.Payment.Accounts).Iban);
        Assert.Equal("58", view.Payment.MeansCode);
        Assert.Equal(document.Number, view.Payment.PaymentReference);
    }

    [Theory]
    [InlineData(EInvoiceFormat.XRechnungUbl, "<cbc:CustomizationID>urn:cen.eu:en16931:2017#compliant#urn:xeinkauf.de:kosit:xrechnung_3.0</cbc:CustomizationID>")]
    [InlineData(EInvoiceFormat.XRechnungCii, "<ram:ID>urn:cen.eu:en16931:2017#compliant#urn:xeinkauf.de:kosit:xrechnung_3.0</ram:ID>")]
    public void XRechnung_carries_the_version_3_specification_identifier(EInvoiceFormat format, string expected)
    {
        var xml = Encoding.UTF8.GetString(writer.Write(TestInvoices.Sample(), format));

        Assert.Contains(expected, xml, StringComparison.Ordinal);
        Assert.Contains(EInvoiceWriter.BusinessProcessBilling, xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Small_business_without_vat_id_uses_tax_number_as_seller_identifier()
    {
        var xml = Encoding.UTF8.GetString(writer.Write(TestInvoices.Sample(smallBusiness: true), EInvoiceFormat.XRechnungUbl));

        Assert.Contains("<cbc:ID>12/345/67890</cbc:ID>", xml, StringComparison.Ordinal);
        Assert.Contains("<cbc:ID>E</cbc:ID>", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("DE123456789", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Buyer_without_leitweg_id_gets_email_as_electronic_address_and_customer_number_as_reference()
    {
        var document = TestInvoices.Sample() with
        {
            Buyer = TestInvoices.Sample().Buyer with { LeitwegId = null },
        };

        var view = reader.Read(writer.Write(document, EInvoiceFormat.XRechnungUbl)).View;

        Assert.Equal("K-0007", view.BuyerReference);
        Assert.False(view.BuyerReferenceIsLeitwegId);
        Assert.Equal("einkauf@musterhausen.example", view.Buyer.ElectronicAddress);
        Assert.Equal("E-Mail", view.Buyer.ElectronicAddressLabel);
    }
}
