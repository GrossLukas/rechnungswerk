using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace RechnungsWerk.Core.Mail;

public enum SmtpSecurity
{
    /// <summary>Unverschlüsselt verbinden, dann per STARTTLS verschlüsseln (meist Port 587).</summary>
    StartTls,

    /// <summary>Sofort verschlüsselt verbinden (SSL/TLS, meist Port 465).</summary>
    SslOnConnect,

    /// <summary>Ohne Verschlüsselung – nur für Server im eigenen Netz.</summary>
    None,
}

public sealed record SmtpConnectionSettings
{
    public required string Host { get; init; }
    public int Port { get; init; } = 587;
    public SmtpSecurity Security { get; init; } = SmtpSecurity.StartTls;
    public string? UserName { get; init; }
    public string? Password { get; init; }
    public required string SenderName { get; init; }
    public required string SenderEmail { get; init; }
}

public sealed record MailAttachment(string FileName, string ContentType, byte[] Content);

public sealed record OutgoingMail(string ToAddress, string? ToName, string Subject, string Body, IReadOnlyList<MailAttachment> Attachments);

/// <summary>Versand ist fehlgeschlagen; <see cref="Exception.Message"/> ist eine verständliche deutsche Erklärung.</summary>
public sealed class MailSendException(string message, Exception? innerException) : Exception(message, innerException);

/// <summary>Versendet Mails über den SMTP-Server, den die Nutzerin oder der Nutzer eingetragen hat.</summary>
public sealed class SmtpMailSender(ILogger<SmtpMailSender> logger)
{
    public async Task SendAsync(SmtpConnectionSettings settings, OutgoingMail mail, CancellationToken cancellationToken = default)
    {
        var message = BuildMessage(settings, mail);

        using var client = new SmtpClient { Timeout = 30_000 };
        try
        {
            await client.ConnectAsync(settings.Host.Trim(), settings.Port, Map(settings.Security), cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(settings.UserName))
            {
                await client.AuthenticateAsync(settings.UserName.Trim(), settings.Password ?? "", cancellationToken).ConfigureAwait(false);
            }

            await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Nur Art und Statuscode protokollieren: Servermeldungen können Kontodaten enthalten.
            var status = ex is SmtpCommandException command ? (int)command.StatusCode : (int?)null;
            logger.LogWarning("Mailversand über {Host}:{Port} fehlgeschlagen ({ErrorType}, SMTP-Status {Status})",
                settings.Host, settings.Port, ex.GetType().Name, status);
            throw new MailSendException(SmtpErrorTranslator.Translate(ex, settings), ex);
        }
    }

    internal static MimeMessage BuildMessage(SmtpConnectionSettings settings, OutgoingMail mail)
    {
        if (!MailboxAddress.TryParse(settings.SenderEmail.Trim(), out var sender))
        {
            throw new MailSendException("Die Absenderadresse ist keine gültige E-Mail-Adresse.", null);
        }

        if (!MailboxAddress.TryParse(mail.ToAddress.Trim(), out var recipient))
        {
            throw new MailSendException($"Die Empfängeradresse „{mail.ToAddress}“ ist keine gültige E-Mail-Adresse.", null);
        }

        sender.Name = settings.SenderName.Trim();
        if (!string.IsNullOrWhiteSpace(mail.ToName))
        {
            recipient.Name = mail.ToName.Trim();
        }

        var message = new MimeMessage();
        message.From.Add(sender);
        message.To.Add(recipient);
        message.Subject = mail.Subject.ReplaceLineEndings(" ").Trim();

        var body = new BodyBuilder { TextBody = mail.Body };
        foreach (var attachment in mail.Attachments)
        {
            body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }

        message.Body = body.ToMessageBody();
        return message;
    }

    private static SecureSocketOptions Map(SmtpSecurity security) => security switch
    {
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
        _ => SecureSocketOptions.None,
    };
}
