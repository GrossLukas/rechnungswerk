using Microsoft.AspNetCore.Identity;

namespace RechnungsWerk.Web.Infrastructure;

/// <summary>Deutsche Fehlertexte für Konto und Passwort.</summary>
public sealed class GermanIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Error(nameof(DefaultError), "Es ist ein unbekannter Fehler aufgetreten.");

    public override IdentityError ConcurrencyFailure() =>
        Error(nameof(ConcurrencyFailure), "Das Konto wurde inzwischen geändert. Bitte lade die Seite neu.");

    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "Das bisherige Passwort stimmt nicht.");

    public override IdentityError InvalidToken() => Error(nameof(InvalidToken), "Der Code ist ungültig oder abgelaufen.");

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), "Der Benutzername darf nur Buchstaben, Ziffern und die Zeichen -._@+ enthalten.");

    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), "Die E-Mail-Adresse ist ungültig.");

    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), "Es gibt bereits ein Konto mit dieser E-Mail-Adresse.");

    public override IdentityError DuplicateEmail(string email) =>
        Error(nameof(DuplicateEmail), "Es gibt bereits ein Konto mit dieser E-Mail-Adresse.");

    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), $"Das Passwort muss mindestens {length} Zeichen lang sein.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(nameof(PasswordRequiresUniqueChars), $"Das Passwort muss mindestens {uniqueChars} verschiedene Zeichen enthalten.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Error(nameof(PasswordRequiresNonAlphanumeric), "Das Passwort muss mindestens ein Sonderzeichen enthalten.");

    public override IdentityError PasswordRequiresDigit() =>
        Error(nameof(PasswordRequiresDigit), "Das Passwort muss mindestens eine Ziffer (0–9) enthalten.");

    public override IdentityError PasswordRequiresLower() =>
        Error(nameof(PasswordRequiresLower), "Das Passwort muss mindestens einen Kleinbuchstaben enthalten.");

    public override IdentityError PasswordRequiresUpper() =>
        Error(nameof(PasswordRequiresUpper), "Das Passwort muss mindestens einen Großbuchstaben enthalten.");

    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
