using System.Text;
using System.Xml.Linq;
using RechnungsWerk.Core.EInvoice;
using RechnungsWerk.Core.Invoicing;

namespace RechnungsWerk.Tests;

public sealed class EInvoiceRoundTripTests
{
    private static readonly XNamespace Rsm = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";
    private static readonly XNamespace Ram = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";
    private static readonly XNamespace Udt = "urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100";

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

    [Theory]
    [InlineData(EInvoiceFormat.XRechnungCii)]
    [InlineData(EInvoiceFormat.ZugferdPdf)]
    public void Service_period_keeps_the_mandatory_cii_delivery_element(EInvoiceFormat format)
    {
        var xml = reader.ExtractXml(writer.Write(TestInvoices.WithServicePeriod(), format));

        var transaction = SecureXml.Load(xml).Root?.Element(Rsm + "SupplyChainTradeTransaction");
        Assert.NotNull(transaction);
        // Das CII-Schema verlangt die Lieferangaben zwischen Vereinbarung und Abrechnung – auch wenn sie leer sind.
        Assert.Equal(
            ["ApplicableHeaderTradeAgreement", "ApplicableHeaderTradeDelivery", "ApplicableHeaderTradeSettlement"],
            transaction.Elements().Select(e => e.Name.LocalName).Where(name => name.StartsWith("ApplicableHeaderTrade", StringComparison.Ordinal)));

        // Kein erfundenes Lieferdatum und keine Lieferanschrift: Der Zeitraum steht als Abrechnungszeitraum (BG-14) in der Abrechnung.
        Assert.Empty(transaction.Element(Ram + "ApplicableHeaderTradeDelivery")!.Elements());
        var period = transaction.Element(Ram + "ApplicableHeaderTradeSettlement")?.Element(Ram + "BillingSpecifiedPeriod");
        Assert.Equal(["20261001", "20261031"], period?.Descendants(Udt + "DateTimeString").Select(d => d.Value) ?? []);
    }

    [Theory]
    [MemberData(nameof(AllFormats))]
    public void Service_period_reads_back_without_a_delivery_date(EInvoiceFormat format, bool smallBusiness)
    {
        var document = TestInvoices.WithServicePeriod(smallBusiness);

        var view = reader.Read(writer.Write(document, format)).View;

        Assert.Null(view.DeliveryDate);
        Assert.Equal(document.ServicePeriodStart, view.PeriodStart);
        Assert.Equal(document.ServicePeriodEnd, view.PeriodEnd);
    }
}
