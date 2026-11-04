using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RechnungsWerk.Core.EInvoice;
using RechnungsWerk.Core.Formatting;
using RechnungsWerk.Core.Invoicing;
using RechnungsWerk.Web.Data;
using RechnungsWerk.Web.Infrastructure;

namespace RechnungsWerk.Web.Services;

public sealed record GeneratedFile(string FileName, string ContentType, byte[] Content);

public sealed class InvoiceService(IDbContextFactory<AppDbContext> dbFactory, EInvoiceWriter writer, TimeProvider time)
{
    // Ein Prozess, eine SQLite-Datei: die Sperre hält den Nummernkreis lückenlos und ohne Doppelvergabe.
    private static readonly SemaphoreSlim NumberGate = new(1, 1);

    public async Task<List<Invoice>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Invoices.AsNoTracking()
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Invoice?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Invoices.AsNoTracking().Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    /// <summary>Vorschlag für eine neue Rechnung: heute, Fälligkeit nach dem Zahlungsziel aus dem Firmenprofil.</summary>
    public InvoiceForm NewForm(CompanyProfile profile)
    {
        var today = Display.Today(time);
        return new InvoiceForm
        {
            IssueDate = today,
            DeliveryDate = today,
            DueDate = today.AddDays(profile.DefaultPaymentDays),
            PreferredFormat = profile.DefaultFormat,
            Lines = [new InvoiceLineForm { VatRate = profile.IsSmallBusiness ? 0 : 19 }],
        };
    }

    public static string FormatNumber(string? prefix, int counter, int year) =>
        (prefix ?? "").Replace("{Jahr}", year.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
        + counter.ToString("0000", CultureInfo.InvariantCulture);

    public static string DefaultPaymentTerms(DateOnly? dueDate) =>
        dueDate is { } due ? $"Zahlbar ohne Abzug bis zum {GermanFormat.Date(due)}." : "Zahlbar sofort ohne Abzug.";

    public async Task<ServiceResult> SaveAsync(InvoiceForm form, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.CompanyProfiles.FirstOrDefaultAsync(p => p.Id == CompanyProfile.SingletonId, cancellationToken);
        if (profile is null)
        {
            return ServiceResult.Fail("Lege zuerst dein Firmenprofil an.");
        }

        var errors = form.Validate(profile.IsSmallBusiness);
        var customer = form.CustomerId is { } customerId
            ? await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            : null;
        if (form.CustomerId is not null && customer is null)
        {
            errors.Add(new FieldError(nameof(InvoiceForm.CustomerId), "Den gewählten Kunden gibt es nicht mehr."));
        }

        if (errors.Count > 0 || customer is null)
        {
            return ServiceResult.Fail(errors);
        }

        var now = time.GetUtcNow().UtcDateTime;
        if (form.Id is null)
        {
            await NumberGate.WaitAsync(cancellationToken);
            try
            {
                var number = FormatNumber(profile.InvoiceNumberPrefix, profile.NextInvoiceNumber, form.IssueDate.Year);
                if (await db.Invoices.AnyAsync(i => i.Number == number, cancellationToken))
                {
                    return ServiceResult.Fail($"Die Rechnungsnummer {number} ist schon vergeben. Passe den Nummernkreis in den Einstellungen an.");
                }

                var invoice = new Invoice
                {
                    Number = number,
                    CreatedAt = now,
                    Seller = profile.ToSellerInfo(),
                    Buyer = customer.ToBuyerInfo(),
                };
                Apply(form, invoice, profile, customer, now);
                db.Invoices.Add(invoice);
                profile.NextInvoiceNumber++;
                await db.SaveChangesAsync(cancellationToken);
                return ServiceResult.Ok(invoice.Id);
            }
            finally
            {
                NumberGate.Release();
            }
        }

        var existing = await db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == form.Id, cancellationToken);
        if (existing is null)
        {
            return ServiceResult.Fail("Diese Rechnung gibt es nicht mehr.");
        }

        if (existing.IsLocked)
        {
            return ServiceResult.Fail("Diese Rechnung wurde bereits versendet und kann nicht mehr geändert werden. Erstelle bei Fehlern eine neue Rechnung.");
        }

        db.InvoiceLines.RemoveRange(existing.Lines);
        existing.Lines = [];
        Apply(form, existing, profile, customer, now);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok(existing.Id);
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (invoice is null)
        {
            return ServiceResult.Ok();
        }

        if (invoice.IsLocked)
        {
            return ServiceResult.Fail("Versendete Rechnungen kannst du nicht löschen.");
        }

        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<GeneratedFile?> RenderAsync(int id, EInvoiceFormat format, CancellationToken cancellationToken = default)
    {
        var invoice = await GetAsync(id, cancellationToken);
        return invoice is null ? null : Render(invoice, format);
    }

    public GeneratedFile Render(Invoice invoice, EInvoiceFormat format) =>
        new(format.FileName(invoice.Number), format.ContentType(), writer.Write(invoice.ToDocument(), format));

    private static void Apply(InvoiceForm form, Invoice invoice, CompanyProfile profile, Customer customer, DateTime now)
    {
        var smallBusiness = profile.IsSmallBusiness;
        invoice.IssueDate = form.IssueDate;
        invoice.DeliveryDate = form.ServiceMode == ServiceDateMode.Date ? form.DeliveryDate : null;
        invoice.ServicePeriodStart = form.ServiceMode == ServiceDateMode.Period ? form.ServicePeriodStart : null;
        invoice.ServicePeriodEnd = form.ServiceMode == ServiceDateMode.Period ? form.ServicePeriodEnd : null;
        invoice.DueDate = form.DueDate;
        invoice.PaymentTerms = FormRules.Clean(form.PaymentTerms) ?? DefaultPaymentTerms(form.DueDate);
        invoice.BuyerReference = FormRules.Clean(form.BuyerReference);
        invoice.OrderReference = FormRules.Clean(form.OrderReference);
        invoice.Note = FormRules.Clean(form.Note);
        invoice.TaxExemptionReason = smallBusiness ? null : FormRules.Clean(form.TaxExemptionReason);
        invoice.PreferredFormat = form.PreferredFormat;
        invoice.CustomerId = customer.Id;
        invoice.CustomerName = customer.Name;
        invoice.Seller = profile.ToSellerInfo();
        invoice.Buyer = customer.ToBuyerInfo();
        invoice.UpdatedAt = now;
        invoice.Lines = form.ToItems(smallBusiness)
            .Select((item, index) => new InvoiceLine
            {
                Position = index + 1,
                Name = item.Name,
                Description = item.Description,
                Quantity = item.Quantity,
                UnitCode = item.UnitCode,
                UnitPrice = item.UnitPrice,
                VatRate = item.VatRate,
            })
            .ToList();

        invoice.UpdateTotals();
    }
}
