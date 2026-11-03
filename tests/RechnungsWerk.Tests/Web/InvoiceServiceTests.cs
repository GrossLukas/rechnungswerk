using Microsoft.EntityFrameworkCore;
using RechnungsWerk.Core.EInvoice;
using RechnungsWerk.Core.Invoicing;
using RechnungsWerk.Web.Data;
using RechnungsWerk.Web.Services;

namespace RechnungsWerk.Tests.Web;

public sealed class InvoiceServiceTests
{
    private static readonly TimeProvider Time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero));

    private static async Task<int> SeedAsync(TestDatabase database, bool smallBusiness = false)
    {
        await using var db = database.Create();
        var seller = TestInvoices.Seller(smallBusiness);
        db.CompanyProfiles.Add(new CompanyProfile
        {
            Name = seller.Name,
            Street = seller.Street,
            PostalCode = seller.PostalCode,
            City = seller.City,
            VatId = seller.VatId,
            TaxNumber = seller.TaxNumber,
            ContactName = seller.ContactName,
            Phone = seller.Phone,
            Email = seller.Email,
            Iban = seller.Iban,
            Bic = seller.Bic,
            BankName = seller.BankName,
            IsSmallBusiness = smallBusiness,
        });
        var customer = new Customer
        {
            Name = "Stadt Musterhausen",
            Street = "Rathausplatz 1",
            PostalCode = "20095",
            City = "Hamburg",
            Email = "einkauf@musterhausen.example",
            LeitwegId = "04011000-1234512345-06",
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer.Id;
    }

    private static InvoiceForm Form(int customerId) => new()
    {
        CustomerId = customerId,
        IssueDate = new DateOnly(2026, 11, 3),
        DeliveryDate = new DateOnly(2026, 10, 31),
        DueDate = new DateOnly(2026, 11, 17),
        Lines =
        [
            new InvoiceLineForm { Name = "Beratung", Quantity = 7.5m, UnitCode = "HUR", UnitPrice = 95m, VatRate = 19 },
            new InvoiceLineForm { Name = "Fachbuch", Quantity = 3m, UnitCode = "C62", UnitPrice = 0.335m, VatRate = 7 },
        ],
    };

    private static InvoiceService Service(TestDatabase database) => new(database.Factory, new EInvoiceWriter(), Time);

    [Fact]
    public async Task Invoice_numbers_are_sequential_and_totals_match_the_generated_xrechnung()
    {
        using var database = new TestDatabase();
        var customerId = await SeedAsync(database);
        var service = Service(database);
        var ct = TestContext.Current.CancellationToken;

        var first = await service.SaveAsync(Form(customerId), ct);
        var second = await service.SaveAsync(Form(customerId), ct);

        Assert.True(first.Succeeded, string.Join(", ", first.Errors));
        Assert.True(second.Succeeded, string.Join(", ", second.Errors));
        var invoice = await service.GetAsync(first.Id!.Value, ct);
        Assert.Equal("RE-2026-0001", invoice!.Number);
        Assert.Equal("RE-2026-0002", (await service.GetAsync(second.Id!.Value, ct))!.Number);
        await using (var db = database.Create())
        {
            Assert.Equal(3, (await db.CompanyProfiles.SingleAsync(ct)).NextInvoiceNumber);
        }

        // 7,5 × 95 = 712,50 (19 %) und 3 × 0,335 = 1,01 (7 %, kaufmännisch gerundet)
        Assert.Equal(713.51m, invoice.NetTotal);
        Assert.Equal(713.51m + 135.38m + 0.07m, invoice.GrandTotal);
        Assert.Equal("Zahlbar ohne Abzug bis zum 17.11.2026.", invoice.PaymentTerms);

        var file = service.Render(invoice, EInvoiceFormat.XRechnungUbl);
        var view = new EInvoiceReader().Read(file.Content).View;
        Assert.Equal("RE-2026-0001", view.Number);
        Assert.Equal(invoice.GrandTotal, view.Totals.GrandTotal);
        Assert.Equal("04011000-1234512345-06", view.BuyerReference);
        Assert.Equal("RE-2026-0001_xrechnung.xml", file.FileName);
    }

    [Fact]
    public async Task Sent_invoices_can_neither_be_changed_nor_deleted()
    {
        using var database = new TestDatabase();
        var customerId = await SeedAsync(database);
        var service = Service(database);
        var ct = TestContext.Current.CancellationToken;
        var id = (await service.SaveAsync(Form(customerId), ct)).Id!.Value;

        await using (var db = database.Create())
        {
            var stored = await db.Invoices.SingleAsync(i => i.Id == id, ct);
            stored.SentAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var edit = Form(customerId);
        edit.Id = id;
        edit.Note = "geändert";

        Assert.False((await service.SaveAsync(edit, ct)).Succeeded);
        Assert.False((await service.DeleteAsync(id, ct)).Succeeded);
        Assert.Null((await service.GetAsync(id, ct))!.Note);
    }

    [Fact]
    public async Task Small_business_invoices_contain_no_vat()
    {
        using var database = new TestDatabase();
        var customerId = await SeedAsync(database, smallBusiness: true);
        var service = Service(database);
        var ct = TestContext.Current.CancellationToken;

        var result = await service.SaveAsync(Form(customerId), ct);

        var invoice = await service.GetAsync(result.Id!.Value, ct);
        Assert.All(invoice!.Lines, line => Assert.Equal(0m, line.VatRate));
        Assert.Equal(invoice.NetTotal, invoice.GrandTotal);
        var tax = Assert.Single(InvoiceCalculator.Calculate(invoice.ToDocument()).Taxes);
        Assert.Equal(InvoiceCalculator.Exempt, tax.CategoryCode);
        Assert.Equal(InvoiceCalculator.SmallBusinessExemptionReason, tax.ExemptionReason);
    }

    [Fact]
    public async Task Zero_rate_without_exemption_reason_is_rejected_with_a_clear_message()
    {
        using var database = new TestDatabase();
        var customerId = await SeedAsync(database);
        var service = Service(database);
        var form = Form(customerId);
        form.Lines[1].VatRate = 0;

        var result = await service.SaveAsync(form, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("Befreiungsgrund", StringComparison.Ordinal));
    }

    [Fact]
    public void Number_prefix_replaces_the_year_placeholder()
    {
        Assert.Equal("RE-2026-0042", InvoiceService.FormatNumber("RE-{Jahr}-", 42, 2026));
        Assert.Equal("0007", InvoiceService.FormatNumber(null, 7, 2026));
        Assert.Equal("2026/12345", InvoiceService.FormatNumber("{jahr}/", 12345, 2026));
    }
}
