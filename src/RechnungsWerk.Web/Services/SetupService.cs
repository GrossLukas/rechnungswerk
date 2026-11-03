using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RechnungsWerk.Web.Data;
using RechnungsWerk.Web.Infrastructure;

namespace RechnungsWerk.Web.Services;

public sealed class SetupForm
{
    [Display(Name = "Einrichtungscode"), Required(ErrorMessage = FormRules.Required)]
    public string SetupCode { get; set; } = "";

    [Display(Name = "E-Mail-Adresse"), Required(ErrorMessage = FormRules.Required), EmailAddress(ErrorMessage = FormRules.InvalidEmail), StringLength(254, ErrorMessage = FormRules.TooLong)]
    public string Email { get; set; } = "";

    [Display(Name = "Passwort"), Required(ErrorMessage = FormRules.Required), StringLength(200, ErrorMessage = FormRules.TooLong)]
    public string Password { get; set; } = "";

    [Display(Name = "Passwort wiederholen"), Required(ErrorMessage = FormRules.Required), Compare(nameof(Password), ErrorMessage = "Die Passwörter stimmen nicht überein.")]
    public string ConfirmPassword { get; set; } = "";

    public CompanyForm Company { get; set; } = new();
}

/// <summary>Ersteinrichtung: erstes Admin-Konto und Firmenprofil. Danach ist die Einrichtung gesperrt.</summary>
public sealed class SetupService(
    IDbContextFactory<AppDbContext> dbFactory,
    UserManager<AppUser> userManager,
    CompanyService companies,
    SetupCodeStore setupCode)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return !await db.Users.AnyAsync(cancellationToken);
    }

    public async Task<ServiceResult> CompleteAsync(SetupForm form, CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (!await IsSetupRequiredAsync(cancellationToken))
            {
                return ServiceResult.Fail("Die Einrichtung ist bereits abgeschlossen. Melde dich mit deinem Konto an.");
            }

            if (!setupCode.Matches(form.SetupCode))
            {
                return ServiceResult.FailAt(nameof(SetupForm.SetupCode), "Der Code stimmt nicht. Er steht in der Datei setup-code.txt im Datenordner.");
            }

            // Erst das Firmenprofil (beliebig oft wiederholbar), dann das Konto – so bleibt nie ein Konto ohne Profil zurück.
            await companies.SaveAsync(form.Company, cancellationToken);

            var email = form.Email.Trim();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
            var created = await userManager.CreateAsync(user, form.Password);
            if (!created.Succeeded)
            {
                // Passwortregeln am Passwortfeld zeigen, alles andere (z. B. doppelte Adresse) an der E-Mail.
                return ServiceResult.Fail(created.Errors.Select(e => new FieldError(
                    e.Code.StartsWith("Password", StringComparison.Ordinal) ? nameof(SetupForm.Password) : nameof(SetupForm.Email), e.Description)));
            }

            setupCode.Delete();
            return ServiceResult.Ok();
        }
        finally
        {
            Gate.Release();
        }
    }
}
