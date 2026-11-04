using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using RechnungsWerk.Web.Infrastructure;

namespace RechnungsWerk.Tests.Web;

/// <summary>Startet die echte Anwendung mit einem leeren Datenordner im Temp-Verzeichnis.</summary>
public sealed class RechnungsWerkAppFactory : WebApplicationFactory<Program>
{
    public string DataPath { get; } = Path.Combine(Path.GetTempPath(), "rechnungswerk-tests-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(DataPaths.ConfigKey, DataPath);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(DataPath, recursive: true);
        }
        catch (IOException)
        {
            // Aufräumen ist nur nett – ein Rest im Temp-Ordner bricht keinen Test.
        }
    }
}

public sealed class WebSecurityTests(RechnungsWerkAppFactory factory) : IClassFixture<RechnungsWerkAppFactory>
{
    private static readonly string[] PublicRoutes = ["/", "/anzeigen", "/pruefen", "/anmelden", "/einrichtung", "/Error", "/not-found"];

    [Fact]
    public void Every_page_except_the_public_ones_requires_login()
    {
        var pages = typeof(Program).Assembly.GetTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type))
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>().Select(route => (Type: type, route.Template)))
            .ToList();

        Assert.Contains(pages, page => page.Template == "/rechnungen");
        foreach (var (type, template) in pages)
        {
            var requiresLogin = type.GetCustomAttribute<AuthorizeAttribute>(inherit: true) is not null;
            if (PublicRoutes.Contains(template))
            {
                Assert.False(requiresLogin, $"{template} ({type.Name}) soll ohne Anmeldung erreichbar sein.");
            }
            else
            {
                Assert.True(requiresLogin, $"{template} ({type.Name}) zeigt Firmendaten und braucht [Authorize].");
            }
        }
    }

    [Theory]
    [InlineData("/rechnungen")]
    [InlineData("/rechnungen/neu")]
    [InlineData("/rechnungen/1")]
    [InlineData("/rechnungen/1/datei/XRechnungUbl")]
    [InlineData("/kunden")]
    [InlineData("/artikel")]
    [InlineData("/einstellungen/firma")]
    [InlineData("/einstellungen/versand")]
    [InlineData("/einstellungen/konto")]
    public async Task Pages_with_company_data_send_anonymous_visitors_to_the_login(string path)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/anmelden", response.Headers.Location!.IsAbsoluteUri ? response.Headers.Location.AbsolutePath : response.Headers.Location.OriginalString.Split('?')[0]);
    }

    [Fact]
    public async Task Start_page_leads_to_the_setup_while_setup_is_pending()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/einrichtung", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/anzeigen")]
    [InlineData("/pruefen")]
    [InlineData("/einrichtung")]
    public async Task Public_pages_load_with_strict_security_headers(string path)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        var csp = Header(response, "Content-Security-Policy");
        Assert.Contains("script-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.DoesNotContain("unsafe", csp);
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task Pages_offer_light_and_dark_before_the_stylesheet_loads()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var html = await client.GetStringAsync("/anzeigen", TestContext.Current.CancellationToken);

        // Sonst malt der Browser im Dunkelmodus erst weiß, bis app.css geladen ist.
        Assert.Contains("<meta name=\"color-scheme\" content=\"light dark\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_addresses_show_the_not_found_page()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/gibt-es-nicht", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Diese Seite gibt es nicht", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Logout_without_antiforgery_token_is_rejected()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.PostAsync("/abmelden", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void Data_folder_holds_database_keys_and_setup_code_while_setup_is_pending()
    {
        var paths = factory.Services.GetRequiredService<DataPaths>();

        Assert.Equal(Path.GetFullPath(factory.DataPath), paths.Root);
        Assert.True(File.Exists(paths.Database));
        Assert.True(Directory.Exists(paths.Keys));
        Assert.True(File.Exists(paths.SetupCode));
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values)
            ? string.Join(",", values)
            : "";
}
