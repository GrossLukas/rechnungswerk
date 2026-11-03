using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using RechnungsWerk.Core.Mail;
using RechnungsWerk.Web.Services;

namespace RechnungsWerk.Tests.Web;

public sealed class SmtpSettingsServiceTests
{
    private const string Password = "geheimes-App-Passwort-42";

    private static SmtpForm Form(string? password = null) => new()
    {
        Host = "smtp.example.org",
        Port = 587,
        UserName = "rechnung@muster.example",
        NewPassword = password,
        SenderName = "Muster Software GmbH",
        SenderEmail = "rechnung@muster.example",
    };

    [Fact]
    public async Task Password_is_stored_encrypted_and_never_returned_to_the_form()
    {
        using var database = new TestDatabase();
        var service = new SmtpSettingsService(database.Factory, new EphemeralDataProtectionProvider(), TimeProvider.System);
        var ct = TestContext.Current.CancellationToken;

        await service.SaveAsync(Form(Password), ct);

        await using (var db = database.Create())
        {
            var stored = await db.SmtpSettings.SingleAsync(ct);
            Assert.NotNull(stored.ProtectedPassword);
            Assert.DoesNotContain(Password, stored.ProtectedPassword);
            Assert.DoesNotContain("geheim", stored.ProtectedPassword);
        }

        var (form, hasStoredPassword, configured) = await service.LoadFormAsync(ct);
        Assert.True(configured);
        Assert.True(hasStoredPassword);
        Assert.Null(form.NewPassword);

        var settings = await service.LoadAsync(ct);
        Assert.Equal(Password, service.ToConnection(settings!).Password);
    }

    [Fact]
    public async Task Empty_password_field_keeps_the_stored_password_and_remove_clears_it()
    {
        using var database = new TestDatabase();
        var service = new SmtpSettingsService(database.Factory, new EphemeralDataProtectionProvider(), TimeProvider.System);
        var ct = TestContext.Current.CancellationToken;

        await service.SaveAsync(Form(Password), ct);
        await service.SaveAsync(Form(), ct);
        Assert.Equal(Password, service.ToConnection((await service.LoadAsync(ct))!).Password);
        Assert.Equal(Password, (await service.ToConnectionAsync(Form(), ct)).Password);

        var remove = Form();
        remove.RemovePassword = true;
        await service.SaveAsync(remove, ct);
        Assert.Null(service.ToConnection((await service.LoadAsync(ct))!).Password);
    }

    [Fact]
    public async Task Password_encrypted_with_lost_keys_gives_an_understandable_error()
    {
        using var database = new TestDatabase();
        var ct = TestContext.Current.CancellationToken;
        await new SmtpSettingsService(database.Factory, new EphemeralDataProtectionProvider(), TimeProvider.System).SaveAsync(Form(Password), ct);

        // Neue Schlüssel, z. B. weil der Datenordner ohne „keys“ wiederhergestellt wurde.
        var service = new SmtpSettingsService(database.Factory, new EphemeralDataProtectionProvider(), TimeProvider.System);
        var settings = await service.LoadAsync(ct);

        var error = Assert.Throws<MailSendException>(() => service.ToConnection(settings!));
        Assert.Contains("neu ein", error.Message);
    }
}
