using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace RechnungsWerk.Core.Mail;

/// <summary>Übersetzt technische SMTP-Fehler in Hinweise, mit denen man etwas anfangen kann.</summary>
internal static class SmtpErrorTranslator
{
    public static string Translate(Exception exception, SmtpConnectionSettings settings) => exception switch
    {
        MailSendException own => own.Message,
        AuthenticationException auth => AuthenticationHint(auth.Message),
        SmtpCommandException { ErrorCode: SmtpErrorCode.RecipientNotAccepted } command =>
            $"Der Server hat den Empfänger abgelehnt ({(int)command.StatusCode}). Bitte prüfe die E-Mail-Adresse des Kunden.",
        SmtpCommandException { ErrorCode: SmtpErrorCode.SenderNotAccepted } command =>
            $"Der Server akzeptiert die Absenderadresse nicht ({(int)command.StatusCode}). Bei den meisten Anbietern muss sie zum Konto gehören, mit dem du dich anmeldest.",
        SmtpCommandException { ErrorCode: SmtpErrorCode.MessageNotAccepted } command =>
            $"Der Server hat die Nachricht abgelehnt ({(int)command.StatusCode}), etwa weil sie zu groß ist oder als Spam gilt.",
        SmtpCommandException command when IsAuthenticationRequired(command) =>
            "Der Server verlangt eine Anmeldung. Bitte trage Benutzername und Passwort ein.",
        SmtpCommandException command =>
            $"Der Server meldet einen Fehler ({(int)command.StatusCode}): {Shorten(command.Message)}",
        SslHandshakeException =>
            "Die verschlüsselte Verbindung kam nicht zustande. Prüfe Port und Verschlüsselung (465 = SSL/TLS, 587 = STARTTLS) " +
            "und ob der Server ein gültiges Zertifikat hat.",
        SmtpProtocolException =>
            "Der Server hat unerwartet geantwortet. Häufige Ursache: SSL/TLS und STARTTLS vertauscht (465 = SSL/TLS, 587 = STARTTLS).",
        SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData } =>
            $"Der Server „{settings.Host}“ wurde nicht gefunden. Bitte prüfe den Hostnamen.",
        SocketException { SocketErrorCode: SocketError.ConnectionRefused } =>
            $"Der Server „{settings.Host}“ nimmt auf Port {settings.Port} keine Verbindungen an. Stimmt der Port?",
        SocketException =>
            $"Keine Verbindung zu „{settings.Host}“ auf Port {settings.Port}. Oft blockiert eine Firewall den Port.",
        TimeoutException or OperationCanceledException =>
            "Zeitüberschreitung: Der Server hat nicht rechtzeitig geantwortet. Oft blockiert eine Firewall den Port, oder Port und Verschlüsselung passen nicht zusammen.",
        NotSupportedException notSupported when notSupported.Message.Contains("STARTTLS", StringComparison.OrdinalIgnoreCase) =>
            "Der Server bietet kein STARTTLS an. Wähle SSL/TLS (meist Port 465) oder prüfe den Port.",
        NotSupportedException notSupported when notSupported.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase) =>
            "Der Server bietet auf diesem Weg keine Anmeldung an. Viele Server erlauben die Anmeldung nur verschlüsselt – wähle STARTTLS oder SSL/TLS.",
        ServiceNotConnectedException or IOException =>
            "Die Verbindung zum Server wurde unerwartet getrennt. Prüfe Port und Verschlüsselung.",
        _ => $"Der Versand ist fehlgeschlagen: {Shorten(exception.Message)}",
    };

    private static string AuthenticationHint(string serverMessage)
    {
        if (serverMessage.Contains("5.7.139", StringComparison.Ordinal)
            || serverMessage.Contains("SmtpClientAuthentication is disabled", StringComparison.OrdinalIgnoreCase))
        {
            return "Microsoft 365 hat SMTP AUTH für dieses Postfach oder den ganzen Mandanten abgeschaltet. Eine Administratorin oder ein " +
                   "Administrator muss „Authentifiziertes SMTP“ für das Postfach im Microsoft-365-Admin-Center freischalten.";
        }

        if (serverMessage.Contains("Application-specific password", StringComparison.OrdinalIgnoreCase)
            || serverMessage.Contains("5.7.9", StringComparison.Ordinal))
        {
            return "Der Anbieter verlangt ein App-Passwort statt deines normalen Passworts. Bei Google: Bestätigung in zwei Schritten " +
                   "aktivieren und unter „App-Passwörter“ ein Passwort für RechnungsWerk erstellen.";
        }

        if (serverMessage.Contains("basic authentication is disabled", StringComparison.OrdinalIgnoreCase)
            || serverMessage.Contains("5.7.3", StringComparison.Ordinal))
        {
            return "Der Anbieter lässt die Anmeldung mit Benutzername und Passwort nicht zu (z. B. Outlook.com). Nötig ist dort ein " +
                   "App-Passwort oder ein Postfach, das SMTP mit Passwort erlaubt.";
        }

        return "Anmeldung abgelehnt: Benutzername oder Passwort stimmen nicht. Viele Anbieter (z. B. Gmail, Outlook, GMX und web.de mit " +
               "Zwei-Faktor-Anmeldung) verlangen ein App-Passwort statt des normalen Passworts.";
    }

    private static bool IsAuthenticationRequired(SmtpCommandException command) =>
        command.StatusCode == SmtpStatusCode.AuthenticationRequired
        || command.Message.Contains("5.7.0", StringComparison.Ordinal) && command.Message.Contains("auth", StringComparison.OrdinalIgnoreCase);

    private static string Shorten(string message)
    {
        var singleLine = message.ReplaceLineEndings(" ").Trim();
        return singleLine.Length <= 200 ? singleLine : singleLine[..200] + "…";
    }
}
