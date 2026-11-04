using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using RechnungsWerk.Core.EInvoice;
using RechnungsWerk.Core.Invoicing;
using RechnungsWerk.Core.Mail;
using RechnungsWerk.Web.Data;

namespace RechnungsWerk.Web.Services;

/// <summary>Eine Meldung zu einem Formularfeld; ohne Feld gilt sie für das ganze Formular.</summary>
public sealed record FieldError(string? Field, string Message);

/// <summary>Ergebnis einer Aktion mit verständlichen Fehlermeldungen für die Oberfläche.</summary>
public sealed record ServiceResult(bool Succeeded, IReadOnlyList<FieldError> Problems, int? Id = null)
{
    public IReadOnlyList<string> Errors => Problems.Select(p => p.Message).ToList();

    /// <summary>Meldungen ohne Feldbezug, z. B. „Diese Rechnung gibt es nicht mehr“.</summary>
    public IReadOnlyList<string> GeneralErrors => Problems.Where(p => p.Field is null).Select(p => p.Message).ToList();

    public string? ErrorFor(string field) =>
        Problems.Where(p => p.Field == field).Select(p => p.Message).FirstOrDefault();

    public static ServiceResult Ok(int? id = null) => new(true, [], id);

    public static ServiceResult Fail(params string[] errors) => new(false, errors.Select(e => new FieldError(null, e)).ToList());

    public static ServiceResult Fail(IEnumerable<string> errors) => new(false, errors.Select(e => new FieldError(null, e)).ToList());

    public static ServiceResult Fail(IEnumerable<FieldError> problems) => new(false, problems.ToList());

    public static ServiceResult FailAt(string field, string message) => new(false, [new FieldError(field, message)]);
}

internal static partial class FormRules
{
    public const string Required = "Bitte fülle „{0}“ aus.";
    public const string TooLong = "„{0}“ darf höchstens {1} Zeichen lang sein.";
    public const string InvalidEmail = "Bitte gib eine gültige E-Mail-Adresse ein.";
    public const string UnknownCountry = "Gib das Land als zweistelligen Ländercode an, z. B. DE, AT oder GB.";

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"^[0-9]{2,12}(-[0-9A-Za-z]{1,30})?-[0-9]{2}$")]
    public static partial Regex LeitwegId();
}

public sealed class CompanyForm : IValidatableObject
{
    [Display(Name = "Firmenname"), Required(ErrorMessage = FormRules.Required), StringLength(200, ErrorMessage = FormRules.TooLong)]
    public string Name { get; set; } = "";

    [Display(Name = "Straße und Hausnummer"), Required(ErrorMessage = FormRules.Required), StringLength(200, ErrorMessage = FormRules.TooLong)]
    public string Street { get; set; } = "";

    [Display(Name = "PLZ"), Required(ErrorMessage = FormRules.Required), StringLength(20, ErrorMessage = FormRules.TooLong)]
    public string PostalCode { get; set; } = "";

    [Display(Name = "Ort"), Required(ErrorMessage = FormRules.Required), StringLength(100, ErrorMessage = FormRules.TooLong)]
    public string City { get; set; } = "";

    [Display(Name = "Land"), Required(ErrorMessage = FormRules.Required)]
    public string CountryCode { get; set; } = "DE";

    [Display(Name = "USt-IdNr."), StringLength(30, ErrorMessage = FormRules.TooLong)]
    public string? VatId { get; set; }

    [Display(Name = "Steuernummer"), StringLength(30, ErrorMessage = FormRules.TooLong)]
    public string? TaxNumber { get; set; }

    [Display(Name = "Ansprechperson"), Required(ErrorMessage = FormRules.Required), StringLength(200, ErrorMessage = FormRules.TooLong)]
    public string ContactName { get; set; } = "";

    [Display(Name = "Telefon"), Required(ErrorMessage = FormRules.Required), StringLength(50, ErrorMessage = FormRules.TooLong)]
    public string Phone { get; set; } = "";

    [Display(Name = "E-Mail"), Required(ErrorMessage = FormRules.Required), EmailAddress(ErrorMessage = FormRules.InvalidEmail), StringLength(254, ErrorMessage = FormRules.TooLong)]
    public string Email { get; set; } = "";

    [Display(Name = "IBAN"), StringLength(42, ErrorMessage = FormRules.TooLong)]
    public string? Iban { get; set; }

    [Display(Name = "BIC"), StringLength(11, ErrorMessage = FormRules.TooLong)]
    public string? Bic { get; set; }

    [Display(Name = "Bank"), StringLength(100, ErrorMessage = FormRules.TooLong)]
    public string? BankName { get; set; }

    public bool IsSmallBusiness { get; set; }

    [Display(Name = "Vorsatz der Rechnungsnummer"), StringLength(30, ErrorMessage = FormRules.TooLong)]
    public string? InvoiceNumberPrefix { get; set; } = "RE-{Jahr}-";

    [Display(Name = "Nächste laufende Nummer"), Range(1, 99_999_999, ErrorMessage = "Die laufende Nummer muss zwischen {1} und {2} liegen.")]
    public int NextInvoiceNumber { get; set; } = 1;

    [Display(Name = "Zahlungsziel in Tagen"), Range(0, 365, ErrorMessage = "Das Zahlungsziel muss zwischen {1} und {2} Tagen liegen.")]
    public int DefaultPaymentDays { get; set; } = 14;

    public EInvoiceFormat DefaultFormat { get; set; } = EInvoiceFormat.XRechnungUbl;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FormRules.Clean(VatId) is null && FormRules.Clean(TaxNumber) is null)
        {
            yield return new ValidationResult("Gib deine USt-IdNr. oder deine Steuernummer an – eine davon ist Pflicht.", [nameof(VatId), nameof(TaxNumber)]);
        }

        if (FormRules.Clean(Iban) is { } iban && !Core.Invoicing.Iban.IsValid(iban))
        {
            yield return new ValidationResult("Die IBAN ist ungültig. Bitte prüfe sie noch einmal.", [nameof(Iban)]);
        }

        // Dieselbe Codeliste wie beim Schreiben der E-Rechnung – sonst ließe sich die Rechnung später nicht erzeugen.
        if (!EInvoiceWriter.IsKnownCountry(CountryCode))
        {
            yield return new ValidationResult(FormRules.UnknownCountry, [nameof(CountryCode)]);
        }
    }

    public static CompanyForm From(CompanyProfile profile) => new()
    {
        Name = profile.Name,
        Street = profile.Street,
        PostalCode = profile.PostalCode,
        City = profile.City,
        CountryCode = profile.CountryCode,
        VatId = profile.VatId,
        TaxNumber = profile.TaxNumber,
        ContactName = profile.ContactName,
        Phone = profile.Phone,
        Email = profile.Email,
        Iban = profile.Iban,
        Bic = profile.Bic,
        BankName = profile.BankName,
        IsSmallBusiness = profile.IsSmallBusiness,
        InvoiceNumberPrefix = profile.InvoiceNumberPrefix,
        NextInvoiceNumber = profile.NextInvoiceNumber,
        DefaultPaymentDays = profile.DefaultPaymentDays,
        DefaultFormat = profile.DefaultFormat,
    };

    public void ApplyTo(CompanyProfile profile)
    {
        profile.Name = Name.Trim();
        profile.Street = Street.Trim();
        profile.PostalCode = PostalCode.Trim();
        profile.City = City.Trim();
        profile.CountryCode = CountryCode.Trim().ToUpperInvariant();
        profile.VatId = FormRules.Clean(VatId)?.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        profile.TaxNumber = FormRules.Clean(TaxNumber);
        profile.ContactName = ContactName.Trim();
        profile.Phone = Phone.Trim();
        profile.Email = Email.Trim();
        profile.Iban = FormRules.Clean(Iban) is { } iban ? Core.Invoicing.Iban.Normalize(iban) : null;
        profile.Bic = FormRules.Clean(Bic)?.ToUpperInvariant();
        profile.BankName = FormRules.Clean(BankName);
        profile.IsSmallBusiness = IsSmallBusiness;
        profile.InvoiceNumberPrefix = InvoiceNumberPrefix?.Trim() ?? "";
        profile.NextInvoiceNumber = NextInvoiceNumber;
        profile.DefaultPaymentDays = DefaultPaymentDays;
        profile.DefaultFormat = DefaultFormat;
    }
}

public sealed class CustomerForm : IValidatableObject
{
    [Display(Name = "Name"), Required(ErrorMessage = FormRules.Required), StringLength(200, ErrorMessage = FormRules.TooLong)]
    public string Name { get; set; } = "";

    [Display(Name = "Straße und Hausnummer"), Required(ErrorMessage = FormRules.Required), StringLength(200, ErrorMessage = FormRules.TooLong)]
    public string Street { get; set; } = "";

    [Display(Name = "PLZ"), Required(ErrorMessage = FormRules.Required), StringLength(20, ErrorMessage = FormRules.TooLong)]
    public string PostalCode { get; set; } = "";

    [Display(Name = "Ort"), Required(ErrorMessage = FormRules.Required), StringLength(100, ErrorMessage = FormRules.TooLong)]
    public string City { get; set; } = "";

    [Display(Name = "Land"), Required(ErrorMessage = FormRules.Required)]
    public string CountryCode { get; set; } = "DE";

    [Display(Name = "Ansprechperson"), StringLength(200, ErrorMessage = FormRules.TooLong)]
    public string? ContactName { get; set; }

    [Display(Name = "E-Mail"), EmailAddress(ErrorMessage = FormRules.InvalidEmail), StringLength(254, ErrorMessage = FormRules.TooLong)]
    public string? Email { get; set; }

    [Display(Name = "Leitweg-ID"), StringLength(46, ErrorMessage = FormRules.TooLong)]
    public string? LeitwegId { get; set; }

    [Display(Name = "Käuferreferenz"), StringLength(100, ErrorMessage = FormRules.TooLong)]
    public string? BuyerReference { get; set; }

    [Display(Name = "USt-IdNr."), StringLength(30, ErrorMessage = FormRules.TooLong)]
    public string? VatId { get; set; }

    [Display(Name = "Kundennummer"), StringLength(50, ErrorMessage = FormRules.TooLong)]
    public string? CustomerNumber { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FormRules.Clean(Email) is null && FormRules.Clean(LeitwegId) is null)
        {
            yield return new ValidationResult("Gib eine E-Mail-Adresse oder eine Leitweg-ID an – sonst kann die E-Rechnung nicht zugestellt werden.",
                [nameof(Email), nameof(LeitwegId)]);
        }

        if (FormRules.Clean(LeitwegId) is { } leitwegId && !FormRules.LeitwegId().IsMatch(leitwegId))
        {
            yield return new ValidationResult("Die Leitweg-ID hat nicht das übliche Format, z. B. 04011000-1234512345-06.", [nameof(LeitwegId)]);
        }

        // Dieselbe Codeliste wie beim Schreiben der E-Rechnung – sonst ließe sich die Rechnung später nicht erzeugen.
        if (!EInvoiceWriter.IsKnownCountry(CountryCode))
        {
            yield return new ValidationResult(FormRules.UnknownCountry, [nameof(CountryCode)]);
        }
    }

    public static CustomerForm From(Customer customer) => new()
    {
        Name = customer.Name,
        Street = customer.Street,
        PostalCode = customer.PostalCode,
        City = customer.City,
        CountryCode = customer.CountryCode,
        ContactName = customer.ContactName,
        Email = customer.Email,
        LeitwegId = customer.LeitwegId,
        BuyerReference = customer.BuyerReference,
        VatId = customer.VatId,
        CustomerNumber = customer.CustomerNumber,
    };

    public void ApplyTo(Customer customer)
    {
        customer.Name = Name.Trim();
        customer.Street = Street.Trim();
        customer.PostalCode = PostalCode.Trim();
        customer.City = City.Trim();
        customer.CountryCode = CountryCode.Trim().ToUpperInvariant();
        customer.ContactName = FormRules.Clean(ContactName);
        customer.Email = FormRules.Clean(Email);
        customer.LeitwegId = FormRules.Clean(LeitwegId);
        customer.BuyerReference = FormRules.Clean(BuyerReference);
        customer.VatId = FormRules.Clean(VatId)?.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        customer.CustomerNumber = FormRules.Clean(CustomerNumber);
    }
}

public sealed class ArticleForm : IValidatableObject
{
    [Display(Name = "Bezeichnung"), Required(ErrorMessage = FormRules.Required), StringLength(200, ErrorMessage = FormRules.TooLong)]
    public string Name { get; set; } = "";

    [Display(Name = "Beschreibung"), StringLength(1000, ErrorMessage = FormRules.TooLong)]
    public string? Description { get; set; }

    [Display(Name = "Einheit"), Required(ErrorMessage = FormRules.Required)]
    public string UnitCode { get; set; } = "C62";

    [Display(Name = "Nettopreis"), Range(typeof(decimal), "0", "999999999", ErrorMessage = "Der Preis muss zwischen 0 und 999.999.999 liegen.")]
    public decimal UnitPrice { get; set; }

    public int VatRate { get; set; } = 19;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!UnitCodes.IsKnown(UnitCode))
        {
            yield return new ValidationResult("Bitte wähle eine Einheit aus der Liste.", [nameof(UnitCode)]);
        }

        if (!VatRates.IsAllowed(VatRate))
        {
            yield return new ValidationResult("Bitte wähle 19 %, 7 % oder 0 %.", [nameof(VatRate)]);
        }

        if (decimal.Round(UnitPrice, 4) != UnitPrice)
        {
            yield return new ValidationResult("Der Preis darf höchstens vier Nachkommastellen haben.", [nameof(UnitPrice)]);
        }
    }

    public static ArticleForm From(Article article) => new()
    {
        Name = article.Name,
        Description = article.Description,
        UnitCode = article.UnitCode,
        UnitPrice = Decimals.Normalize(article.UnitPrice),
        VatRate = VatRates.ToPercent(article.VatRate),
    };

    public void ApplyTo(Article article)
    {
        article.Name = Name.Trim();
        article.Description = FormRules.Clean(Description);
        article.UnitCode = UnitCode.Trim().ToUpperInvariant();
        article.UnitPrice = UnitPrice;
        article.VatRate = VatRate;
    }
}

public static class VatRates
{
    public static readonly IReadOnlyList<int> Allowed = [19, 7, 0];

    public static bool IsAllowed(int rate) => Allowed.Contains(rate);

    /// <summary>Gespeicherte Sätze sind ganze Prozente (19, 7, 0).</summary>
    public static int ToPercent(decimal rate) => (int)decimal.Round(rate, MidpointRounding.AwayFromZero);
}

public enum ServiceDateMode
{
    Date,
    Period,
}

public sealed class InvoiceForm
{
    public int? Id { get; set; }
    public int? CustomerId { get; set; }
    public DateOnly IssueDate { get; set; }
    public ServiceDateMode ServiceMode { get; set; } = ServiceDateMode.Date;
    public DateOnly? DeliveryDate { get; set; }
    public DateOnly? ServicePeriodStart { get; set; }
    public DateOnly? ServicePeriodEnd { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? PaymentTerms { get; set; }
    public string? BuyerReference { get; set; }
    public string? OrderReference { get; set; }
    public string? Note { get; set; }
    public string? TaxExemptionReason { get; set; }
    public EInvoiceFormat PreferredFormat { get; set; } = EInvoiceFormat.XRechnungUbl;
    public List<InvoiceLineForm> Lines { get; set; } = [];

    public IEnumerable<InvoiceLineItem> ToItems(bool smallBusiness) =>
        Lines.Select(l => new InvoiceLineItem
        {
            Name = l.Name.Trim(),
            Description = FormRules.Clean(l.Description),
            Quantity = l.Quantity,
            UnitCode = l.UnitCode,
            UnitPrice = l.UnitPrice,
            VatRate = smallBusiness ? 0m : l.VatRate,
        });

    /// <summary>Fachliche Prüfung vor dem Speichern; jede Meldung gehört zu einem Feld (Schlüssel wie „Lines[0].Name“).</summary>
    public List<FieldError> Validate(bool smallBusiness)
    {
        var errors = new List<FieldError>();
        void Add(string field, string message) => errors.Add(new FieldError(field, message));

        if (CustomerId is null)
        {
            Add(nameof(CustomerId), "Bitte wähle einen Kunden.");
        }

        if (ServiceMode == ServiceDateMode.Date && DeliveryDate is null)
        {
            Add(nameof(DeliveryDate), "Bitte gib das Leistungsdatum an.");
        }

        if (ServiceMode == ServiceDateMode.Period)
        {
            if (ServicePeriodStart is null || ServicePeriodEnd is null)
            {
                Add(nameof(ServicePeriodStart), "Bitte gib Beginn und Ende der Leistung an.");
            }
            else if (ServicePeriodStart > ServicePeriodEnd)
            {
                Add(nameof(ServicePeriodEnd), "Das Ende liegt vor dem Beginn.");
            }
        }

        if (DueDate is { } due && due < IssueDate)
        {
            Add(nameof(DueDate), "Die Fälligkeit liegt vor dem Rechnungsdatum.");
        }

        if (Lines.Count == 0)
        {
            Add(nameof(Lines), "Die Rechnung braucht mindestens eine Position.");
        }

        for (var i = 0; i < Lines.Count; i++)
        {
            var line = Lines[i];
            var prefix = $"{nameof(Lines)}[{i}].";
            if (string.IsNullOrWhiteSpace(line.Name))
            {
                Add(prefix + nameof(line.Name), "Bitte gib eine Bezeichnung ein.");
            }
            else if (line.Name.Trim().Length > 200)
            {
                Add(prefix + nameof(line.Name), "Die Bezeichnung darf höchstens 200 Zeichen lang sein.");
            }

            if (line.Quantity <= 0 || line.Quantity > 1_000_000_000m || decimal.Round(line.Quantity, 4) != line.Quantity)
            {
                Add(prefix + nameof(line.Quantity), "Die Menge muss größer als 0 sein (höchstens vier Nachkommastellen).");
            }

            if (line.UnitPrice < 0 || line.UnitPrice > 999_999_999m || decimal.Round(line.UnitPrice, 4) != line.UnitPrice)
            {
                Add(prefix + nameof(line.UnitPrice), "Der Preis darf nicht negativ sein (höchstens vier Nachkommastellen).");
            }

            if (!UnitCodes.IsKnown(line.UnitCode))
            {
                Add(prefix + nameof(line.UnitCode), "Bitte wähle eine Einheit aus der Liste.");
            }

            if (!smallBusiness && !VatRates.IsAllowed(line.VatRate))
            {
                Add(prefix + nameof(line.VatRate), "Bitte wähle 19 %, 7 % oder 0 %.");
            }
        }

        if (!smallBusiness && Lines.Any(l => l.VatRate == 0) && FormRules.Clean(TaxExemptionReason) is null)
        {
            Add(nameof(TaxExemptionReason), "Für 0 % Umsatzsteuer brauchst du einen Befreiungsgrund, z. B. „Steuerfrei nach § 4 Nr. 21 UStG“.");
        }

        foreach (var (field, value, max) in new[]
                 {
                     (nameof(Note), Note, 2000),
                     (nameof(PaymentTerms), PaymentTerms, 500),
                     (nameof(BuyerReference), BuyerReference, 100),
                     (nameof(OrderReference), OrderReference, 100),
                     (nameof(TaxExemptionReason), TaxExemptionReason, 300),
                 })
        {
            if (value?.Length > max)
            {
                Add(field, $"Höchstens {max} Zeichen.");
            }
        }

        return errors;
    }

    public static InvoiceForm From(Invoice invoice) => new()
    {
        Id = invoice.Id,
        CustomerId = invoice.CustomerId,
        IssueDate = invoice.IssueDate,
        ServiceMode = invoice.ServicePeriodStart is not null ? ServiceDateMode.Period : ServiceDateMode.Date,
        DeliveryDate = invoice.DeliveryDate,
        ServicePeriodStart = invoice.ServicePeriodStart,
        ServicePeriodEnd = invoice.ServicePeriodEnd,
        DueDate = invoice.DueDate,
        // Der gespeicherte Vorschlag ist keine eigene Eingabe: leer laden, damit er beim Speichern zur Fälligkeit passt.
        PaymentTerms = invoice.PaymentTerms == InvoiceService.DefaultPaymentTerms(invoice.DueDate) ? null : invoice.PaymentTerms,
        BuyerReference = invoice.BuyerReference,
        OrderReference = invoice.OrderReference,
        Note = invoice.Note,
        TaxExemptionReason = invoice.TaxExemptionReason,
        PreferredFormat = invoice.PreferredFormat,
        Lines = invoice.Lines.OrderBy(l => l.Position).Select(l => new InvoiceLineForm
        {
            Name = l.Name,
            Description = l.Description,
            Quantity = Decimals.Normalize(l.Quantity),
            UnitCode = l.UnitCode,
            UnitPrice = Decimals.Normalize(l.UnitPrice),
            VatRate = VatRates.ToPercent(l.VatRate),
        }).ToList(),
    };
}

public sealed class InvoiceLineForm
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Quantity { get; set; } = 1m;
    public string UnitCode { get; set; } = "C62";
    public decimal UnitPrice { get; set; }
    public int VatRate { get; set; } = 19;

    public static InvoiceLineForm FromArticle(Article article) => new()
    {
        Name = article.Name,
        Description = article.Description,
        Quantity = 1m,
        UnitCode = article.UnitCode,
        UnitPrice = Decimals.Normalize(article.UnitPrice),
        VatRate = VatRates.ToPercent(article.VatRate),
    };
}

public sealed class SmtpForm
{
    [Display(Name = "SMTP-Server"), Required(ErrorMessage = FormRules.Required), StringLength(253, ErrorMessage = FormRules.TooLong)]
    public string Host { get; set; } = "";

    [Display(Name = "Port"), Range(1, 65535, ErrorMessage = "Der Port muss zwischen {1} und {2} liegen.")]
    public int Port { get; set; } = 587;

    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;

    [Display(Name = "Benutzername"), StringLength(254, ErrorMessage = FormRules.TooLong)]
    public string? UserName { get; set; }

    /// <summary>Nur zum Ändern: bleibt leer, wenn das gespeicherte Passwort weiter gelten soll.</summary>
    [Display(Name = "Passwort"), StringLength(500, ErrorMessage = FormRules.TooLong)]
    public string? NewPassword { get; set; }

    public bool RemovePassword { get; set; }

    [Display(Name = "Absendername"), Required(ErrorMessage = FormRules.Required), StringLength(200, ErrorMessage = FormRules.TooLong)]
    public string SenderName { get; set; } = "";

    [Display(Name = "Absenderadresse"), Required(ErrorMessage = FormRules.Required), EmailAddress(ErrorMessage = FormRules.InvalidEmail), StringLength(254, ErrorMessage = FormRules.TooLong)]
    public string SenderEmail { get; set; } = "";

    [Display(Name = "Betreff"), Required(ErrorMessage = FormRules.Required), StringLength(300, ErrorMessage = FormRules.TooLong)]
    public string SubjectTemplate { get; set; } = MailTemplate.DefaultSubject;

    [Display(Name = "Text"), Required(ErrorMessage = FormRules.Required), StringLength(10_000, ErrorMessage = FormRules.TooLong)]
    public string BodyTemplate { get; set; } = MailTemplate.DefaultBody;
}

public static class Decimals
{
    /// <summary>Entfernt Nullen hinter dem Komma (SQLite liefert z. B. 95.0) – für ruhige Eingabefelder.</summary>
    public static decimal Normalize(decimal value) =>
        decimal.Parse(value.ToString("0.############", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture);
}
