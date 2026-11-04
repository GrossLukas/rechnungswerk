using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RechnungsWerk.Core.EInvoice;
using RechnungsWerk.Core.Mail;
using RechnungsWerk.Core.Pdf;
using RechnungsWerk.Core.Validation;
using RechnungsWerk.Web.Components;
using RechnungsWerk.Web.Data;
using RechnungsWerk.Web.Endpoints;
using RechnungsWerk.Web.Infrastructure;
using RechnungsWerk.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var dataPaths = DataPaths.FromConfiguration(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(dataPaths);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    // Formulare sind klein; Rechnungsdateien kommen über die Blazor-Verbindung und sind dort auf 10 MB begrenzt.
    options.Limits.MaxRequestBodySize = 2 * 1024 * 1024;
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment())
    // Ein geänderter Feldwert reist in einer Nachricht. Mit den erlaubten 10.000 Zeichen (Mailtext) und Mehrbyte-Zeichen
    // reichten die standardmäßigen 32 KB nicht: Die Verbindung brach ab, und der Wert ging still verloren.
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 128 * 1024);

builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(dataPaths.ConnectionString));
builder.Services.TryAddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

builder.Services.AddDataProtection()
    .SetApplicationName("RechnungsWerk")
    .PersistKeysToFileSystem(new DirectoryInfo(dataPaths.Keys));

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();
builder.Services.AddAuthorization();
builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 12;
        options.Password.RequiredUniqueChars = 5;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddErrorDescriber<GermanIdentityErrorDescriber>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "rechnungswerk.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/anmelden";
    options.AccessDeniedPath = "/anmelden";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});
// Sicherheitsstempel bei jeder Anfrage prüfen: Nach Abmelden oder Passwortwechsel gilt ein altes Cookie sofort nicht mehr.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
builder.Services.AddSingleton<SignOutBroadcast>();
builder.Services.AddScoped<CircuitHandler, SignOutCircuitHandler>();
builder.Services.AddAntiforgery(options => options.Cookie.Name = "rechnungswerk.antiforgery");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<EInvoiceReader>();
builder.Services.AddSingleton<InvoicePdfRenderer>();
builder.Services.AddSingleton<EInvoiceWriter>();
builder.Services.AddSingleton<SmtpMailSender>();

var validatorOptions = builder.Configuration.GetSection(ValidatorOptions.SectionName).Get<ValidatorOptions>() ?? new ValidatorOptions();
builder.Services.AddSingleton(validatorOptions);
builder.Services.AddHttpClient<KositValidatorClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, validatorOptions.TimeoutSeconds) + 10));

builder.Services.AddSingleton<SetupCodeStore>();
builder.Services.AddScoped<SetupService>();
builder.Services.AddScoped<CompanyService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<ArticleService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<SmtpSettingsService>();
builder.Services.AddScoped<InvoiceMailService>();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseSecurityHeaders();
// Fehlerseiten nur für Seitenaufrufe: Ein erneuter Durchlauf eines abgelehnten POST (z. B. ohne Antiforgery-Token)
// würde das Formular ein zweites Mal prüfen und mit einem Serverfehler statt 400 enden.
app.UseWhen(
    context => HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));

// Routing ausdrücklich nach den Fehlerseiten, damit ein erneuter Durchlauf (/Error, /not-found) wieder geroutet wird.
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Abgelaufenes oder fremdes Antiforgery-Token (zweiter Tab, Sitzung abgelaufen): zurück auf eine frische Seite statt in
// eine leere 400-Seite. Ausgeführt wird dabei nichts – Angemeldete bleiben angemeldet und gehen zur Startseite.
app.Use(async (context, next) =>
{
    if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false })
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = context.User.Identity?.IsAuthenticated == true ? "/"
            : context.Request.Path.StartsWithSegments("/anmelden") ? $"{context.Request.Path}{context.Request.QueryString}"
            : "/anmelden";
        return;
    }

    await next(context);
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapAppEndpoints();

app.Run();

public partial class Program;
