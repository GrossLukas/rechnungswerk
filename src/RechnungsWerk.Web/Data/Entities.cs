using Microsoft.AspNetCore.Identity;
using RechnungsWerk.Core.EInvoice;
using RechnungsWerk.Core.Invoicing;
using RechnungsWerk.Core.Mail;

namespace RechnungsWerk.Web.Data;

public sealed class AppUser : IdentityUser;

/// <summary>Firmenprofil des Rechnungsstellers. Es gibt genau einen Datensatz.</summary>
public sealed class CompanyProfile
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string Name { get; set; } = "";
    public string Street { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string City { get; set; } = "";
    public string CountryCode { get; set; } = "DE";
    public string? VatId { get; set; }
    public string? TaxNumber { get; set; }
    public string ContactName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Iban { get; set; }
    public string? Bic { get; set; }
    public string? BankName { get; set; }
    public bool IsSmallBusiness { get; set; }

    /// <summary>Vorsatz des Nummernkreises; {Jahr} wird durch das Jahr des Rechnungsdatums ersetzt.</summary>
    public string InvoiceNumberPrefix { get; set; } = "RE-{Jahr}-";

    public int NextInvoiceNumber { get; set; } = 1;
    public int DefaultPaymentDays { get; set; } = 14;
    public EInvoiceFormat DefaultFormat { get; set; } = EInvoiceFormat.XRechnungUbl;
    public DateTime UpdatedAt { get; set; }

    public SellerInfo ToSellerInfo() => new()
    {
        Name = Name,
        Street = Street,
        PostalCode = PostalCode,
        City = City,
        CountryCode = CountryCode,
        VatId = Blank(VatId),
        TaxNumber = Blank(TaxNumber),
        ContactName = ContactName,
        Phone = Phone,
        Email = Email,
        Iban = Blank(Iban),
        Bic = Blank(Bic),
        BankName = Blank(BankName),
        IsSmallBusiness = IsSmallBusiness,
    };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Street { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string City { get; set; } = "";
    public string CountryCode { get; set; } = "DE";
    public string? ContactName { get; set; }
    public string? Email { get; set; }

    /// <summary>Leitweg-ID für Rechnungen an Behörden (Käuferreferenz).</summary>
    public string? LeitwegId { get; set; }

    /// <summary>Käuferreferenz, die der Kunde vorgegeben hat (z. B. Kostenstelle).</summary>
    public string? BuyerReference { get; set; }

    public string? VatId { get; set; }
    public string? CustomerNumber { get; set; }
    public DateTime CreatedAt { get; set; }

    public BuyerInfo ToBuyerInfo() => new()
    {
        Name = Name,
        Street = Street,
        PostalCode = PostalCode,
        City = City,
        CountryCode = CountryCode,
        ContactName = Blank(ContactName),
        Email = Blank(Email),
        LeitwegId = Blank(LeitwegId),
        VatId = Blank(VatId),
        CustomerNumber = Blank(CustomerNumber),
    };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class Article
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>Einheit nach UN/ECE Recommendation 20, z. B. C62 (Stück) oder HUR (Stunde).</summary>
    public string UnitCode { get; set; } = "C62";

    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; } = 19m;
    public DateTime CreatedAt { get; set; }
}

public sealed class Invoice
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public DateOnly IssueDate { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public DateOnly? ServicePeriodStart { get; set; }
    public DateOnly? ServicePeriodEnd { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? PaymentTerms { get; set; }
    public string? BuyerReference { get; set; }
    public string? OrderReference { get; set; }
    public string? Note { get; set; }
    public string? TaxExemptionReason { get; set; }
    public string Currency { get; set; } = "EUR";

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Kundenname zum Zeitpunkt der Rechnung – für Liste und Suche.</summary>
    public string CustomerName { get; set; } = "";

    /// <summary>Angaben zum Rechnungssteller, eingefroren beim Speichern (spätere Profiländerungen ändern alte Rechnungen nicht).</summary>
    public required SellerInfo Seller { get; set; }

    /// <summary>Angaben zum Kunden, eingefroren beim Speichern.</summary>
    public required BuyerInfo Buyer { get; set; }

    public decimal NetTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public EInvoiceFormat PreferredFormat { get; set; } = EInvoiceFormat.XRechnungUbl;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public DateTime? SentAt { get; set; }
    public string? SentTo { get; set; }
    public EInvoiceFormat? SentFormat { get; set; }
    public DateTime? LastMailAttemptAt { get; set; }
    public string? LastMailError { get; set; }

    public List<InvoiceLine> Lines { get; set; } = [];

    /// <summary>Versendete Rechnungen gelten als gestellt und werden nicht mehr verändert.</summary>
    public bool IsLocked => SentAt is not null;

    public InvoiceDocument ToDocument() => new()
    {
        Number = Number,
        IssueDate = IssueDate,
        DeliveryDate = DeliveryDate,
        ServicePeriodStart = ServicePeriodStart,
        ServicePeriodEnd = ServicePeriodEnd,
        DueDate = DueDate,
        PaymentTerms = PaymentTerms,
        BuyerReference = BuyerReference,
        OrderReference = OrderReference,
        Note = Note,
        TaxExemptionReason = TaxExemptionReason,
        Currency = Currency,
        Seller = Seller,
        Buyer = Buyer,
        Lines = Lines.OrderBy(l => l.Position).Select(l => l.ToItem()).ToList(),
    };
}

public sealed class InvoiceLine
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int Position { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public string UnitCode { get; set; } = "C62";
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }

    public InvoiceLineItem ToItem() => new()
    {
        Name = Name,
        Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
        Quantity = Quantity,
        UnitCode = UnitCode,
        UnitPrice = UnitPrice,
        VatRate = VatRate,
    };
}

/// <summary>SMTP-Zugang für den Rechnungsversand. Es gibt genau einen Datensatz.</summary>
public sealed class SmtpSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;
    public string? UserName { get; set; }

    /// <summary>Mit DataProtection verschlüsseltes Passwort – niemals im Klartext speichern oder anzeigen.</summary>
    public string? ProtectedPassword { get; set; }

    public string SenderName { get; set; } = "";
    public string SenderEmail { get; set; } = "";
    public string SubjectTemplate { get; set; } = MailTemplate.DefaultSubject;
    public string BodyTemplate { get; set; } = MailTemplate.DefaultBody;
    public DateTime UpdatedAt { get; set; }
}
