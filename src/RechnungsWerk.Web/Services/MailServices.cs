using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using RechnungsWerk.Core.EInvoice;
using RechnungsWerk.Core.Formatting;
using RechnungsWerk.Core.Mail;
using RechnungsWerk.Web.Data;

namespace RechnungsWerk.Web.Services;

/// <summary>
/// Speichert den SMTP-Zugang. Das Passwort wird mit DataProtection verschlüsselt abgelegt, nie an die Oberfläche
/// zurückgegeben und nur für den eigentlichen Versand entschlüsselt.
/// </summary>
public sealed class SmtpSettingsService(IDbContextFactory<AppDbContext> dbFactory, IDataProtectionProvider dataProtection, TimeProvider time)
{
    internal const string ProtectorPurpose = "RechnungsWerk.SmtpPassword.v1";

    private readonly IDataProtector protector = dataProtection.CreateProtector(ProtectorPurpose);

    public async Task<(SmtpForm Form, bool HasStoredPassword, bool IsConfigured)> LoadFormAsync(CancellationToken cancellationToken = default)
    {
        var settings = await LoadAsync(cancellationToken);
        if (settings is null)
        {
            return (new SmtpForm(), false, false);
        }

        var form = new SmtpForm
        {
            Host = settings.Host,
            Port = settings.Port,
            Security = settings.Security,
            UserName = settings.UserName,
            SenderName = settings.SenderName,
            SenderEmail = settings.SenderEmail,
            SubjectTemplate = settings.SubjectTemplate,
            BodyTemplate = settings.BodyTemplate,
        };
        return (form, settings.ProtectedPassword is not null, true);
    }

    public async Task SaveAsync(SmtpForm form, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var settings = await db.SmtpSettings.FirstOrDefaultAsync(s => s.Id == SmtpSettings.SingletonId, cancellationToken);
        if (settings is null)
        {
            settings = new SmtpSettings();
            db.SmtpSettings.Add(settings);
        }

        settings.Host = form.Host.Trim();
        settings.Port = form.Port;
        settings.Security = form.Security;
        settings.UserName = FormRules.Clean(form.UserName);
        settings.SenderName = form.SenderName.Trim();
        settings.SenderEmail = form.SenderEmail.Trim();
        settings.SubjectTemplate = form.SubjectTemplate.Trim();
        settings.BodyTemplate = form.BodyTemplate;
        if (form.RemovePassword)
        {
            settings.ProtectedPassword = null;
        }
        else if (!string.IsNullOrEmpty(form.NewPassword))
        {
            settings.ProtectedPassword = protector.Protect(form.NewPassword);
        }

        settings.UpdatedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
    }

    internal async Task<SmtpSettings?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SmtpSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == SmtpSettings.SingletonId, cancellationToken);
    }

    /// <summary>Verbindungsdaten für den Versand mit entschlüsseltem Passwort.</summary>
    internal SmtpConnectionSettings ToConnection(SmtpSettings settings) => new()
    {
        Host = settings.Host,
        Port = settings.Port,
        Security = settings.Security,
        UserName = settings.UserName,
        Password = Unprotect(settings.ProtectedPassword),
        SenderName = settings.SenderName,
        SenderEmail = settings.SenderEmail,
    };

    /// <summary>Verbindungsdaten aus dem Formular (für die Testmail); ohne neues Passwort gilt das gespeicherte.</summary>
    internal async Task<SmtpConnectionSettings> ToConnectionAsync(SmtpForm form, CancellationToken cancellationToken = default)
    {
        string? password;
        if (form.RemovePassword)
        {
            password = null;
        }
        else if (!string.IsNullOrEmpty(form.NewPassword))
        {
            password = form.NewPassword;
        }
        else
        {
            password = Unprotect((await LoadAsync(cancellationToken))?.ProtectedPassword);
        }

        return new SmtpConnectionSettings
        {
            Host = form.Host.Trim(),
            Port = form.Port,
            Security = form.Security,
            UserName = FormRules.Clean(form.UserName),
            Password = password,
            SenderName = form.SenderName.Trim(),
            SenderEmail = form.SenderEmail.Trim(),
        };
    }

    private string? Unprotect(string? protectedPassword)
    {
        if (protectedPassword is null)
        {
            return null;
        }

        try
        {
            return protector.Unprotect(protectedPassword);
        }
        catch (CryptographicException ex)
        {
            throw new MailSendException(
                "Das gespeicherte SMTP-Passwort lässt sich nicht mehr entschlüsseln – vermutlich fehlen die Schlüssel im Datenordner. " +
                "Bitte trage das Passwort in den Versand-Einstellungen neu ein.", ex);
        }
    }
}

/// <summary>Versendet Rechnungen und Testmails und merkt sich den Versandstatus je Rechnung.</summary>
public sealed class InvoiceMailService(
    IDbContextFactory<AppDbContext> dbFactory,
    InvoiceService invoices,
    SmtpSettingsService smtpSettings,
    SmtpMailSender sender,
    TimeProvider time)
{
    public const string RecipientField = "Recipient";

    private static readonly EmailAddressAttribute EmailCheck = new();

    public async Task<ServiceResult> SendInvoiceAsync(int invoiceId, EInvoiceFormat format, string? recipient, CancellationToken cancellationToken = default)
    {
        var to = recipient?.Trim();
        if (string.IsNullOrEmpty(to) || !EmailCheck.IsValid(to))
        {
            return ServiceResult.FailAt(RecipientField, "Bitte gib eine gültige Empfängeradresse ein.");
        }

        var settings = await smtpSettings.LoadAsync(cancellationToken);
        if (settings is null)
        {
            return ServiceResult.Fail("Richte zuerst den Versand ein (Einstellungen → Versand).");
        }

        var invoice = await invoices.GetAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return ServiceResult.Fail("Diese Rechnung gibt es nicht mehr.");
        }

        if (!invoice.IsLocked && invoice.LacksExemptionReason)
        {
            return ServiceResult.Fail("Für die Positionen mit 0 % Umsatzsteuer fehlt der Befreiungsgrund. Bearbeite die Rechnung und speichere sie, bevor du sie sendest.");
        }

        var values = new MailTemplateValues(
            invoice.Number,
            invoice.CustomerName,
            GermanFormat.Money(invoice.GrandTotal, invoice.Currency),
            invoice.DueDate is { } due ? GermanFormat.Date(due) : null,
            invoice.Seller.Name);
        GeneratedFile file;
        try
        {
            file = invoices.Render(invoice, format);
        }
        catch (EInvoiceWriteException ex)
        {
            return ServiceResult.Fail(ex.Message);
        }

        var mail = new OutgoingMail(
            to,
            invoice.Buyer.ContactName ?? invoice.CustomerName,
            MailTemplate.RenderSubject(settings.SubjectTemplate, values),
            MailTemplate.Render(settings.BodyTemplate, values),
            [new MailAttachment(file.FileName, file.ContentType, file.Content)]);

        string? error = null;
        try
        {
            await sender.SendAsync(smtpSettings.ToConnection(settings), mail, cancellationToken);
        }
        catch (MailSendException ex)
        {
            error = ex.Message;
        }

        await RecordAttemptAsync(invoiceId, to, format, error, cancellationToken);
        return error is null ? ServiceResult.Ok(invoiceId) : ServiceResult.Fail(error);
    }

    public async Task<ServiceResult> SendTestAsync(SmtpForm form, string? recipient, CancellationToken cancellationToken = default)
    {
        var to = recipient?.Trim();
        if (string.IsNullOrEmpty(to) || !EmailCheck.IsValid(to))
        {
            return ServiceResult.FailAt(RecipientField, "Bitte gib eine gültige Adresse ein.");
        }

        try
        {
            var connection = await smtpSettings.ToConnectionAsync(form, cancellationToken);
            var mail = new OutgoingMail(to, null, "Testmail von RechnungsWerk",
                "Hallo,\n\ndiese Testmail zeigt: Der Versand aus RechnungsWerk funktioniert.\n\nViele Grüße\nRechnungsWerk", []);
            await sender.SendAsync(connection, mail, cancellationToken);
            return ServiceResult.Ok();
        }
        catch (MailSendException ex)
        {
            return ServiceResult.Fail(ex.Message);
        }
    }

    private async Task RecordAttemptAsync(int invoiceId, string recipient, EInvoiceFormat format, string? error, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
        if (invoice is null)
        {
            return;
        }

        var now = time.GetUtcNow().UtcDateTime;
        invoice.LastMailAttemptAt = now;
        invoice.LastMailError = error;
        if (error is null)
        {
            invoice.SentAt = now;
            invoice.SentTo = recipient;
            invoice.SentFormat = format;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
