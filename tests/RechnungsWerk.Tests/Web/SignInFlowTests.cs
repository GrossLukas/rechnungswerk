using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RechnungsWerk.Web.Data;

namespace RechnungsWerk.Tests.Web;

/// <summary>Anmelden und Abmelden über die echten Formulare – mit eigener Instanz, damit die Einrichtung dort abgeschlossen ist.</summary>
public sealed partial class SignInFlowTests(RechnungsWerkAppFactory factory) : IClassFixture<RechnungsWerkAppFactory>, IAsyncLifetime
{
    private const string Email = "erika@example.org";
    private const string Password = "lange-passwort-2026";

    public async ValueTask InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        if (await users.FindByEmailAsync(Email) is null)
        {
            var created = await users.CreateAsync(new AppUser { UserName = Email, Email = Email, EmailConfirmed = true }, Password);
            Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Login_from_a_stale_form_goes_back_to_the_login_and_keeps_the_return_url()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tabA = Client();
        using var tabB = Client();
        await TokenAsync(tabA, "/anmelden?ReturnUrl=%2Fkunden", ct);
        var foreignToken = await TokenAsync(tabB, "/anmelden", ct);

        using var response = await tabA.PostAsync("/anmelden?ReturnUrl=%2Fkunden", LoginForm(foreignToken), ct);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal("/anmelden?ReturnUrl=%2Fkunden", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Logout_with_an_invalid_token_keeps_the_user_signed_in()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = await SignedInClientAsync(ct);

        using var response = await client.PostAsync("/abmelden", new FormUrlEncodedContent([]), ct);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal("/", response.Headers.Location!.OriginalString);
        using var page = await client.GetAsync("/einstellungen/konto", ct);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    [Fact]
    public async Task Logout_ends_the_session_in_other_browsers_at_once()
    {
        var ct = TestContext.Current.CancellationToken;
        using var otherBrowser = await SignedInClientAsync(ct);
        using var client = await SignedInClientAsync(ct);
        var token = await TokenAsync(client, "/einstellungen/konto", ct);

        using var logout = await client.PostAsync("/abmelden", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        }), ct);

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        using var page = await otherBrowser.GetAsync("/einstellungen/konto", ct);
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        var location = page.Headers.Location!;
        Assert.Equal("/anmelden", location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString.Split('?')[0]);
    }

    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<HttpClient> SignedInClientAsync(CancellationToken ct)
    {
        var client = Client();
        var token = await TokenAsync(client, "/anmelden", ct);
        using var response = await client.PostAsync("/anmelden", LoginForm(token), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static FormUrlEncodedContent LoginForm(string token) => new(new Dictionary<string, string>
    {
        ["_handler"] = "login",
        ["Input.Email"] = Email,
        ["Input.Password"] = Password,
        ["__RequestVerificationToken"] = token,
    });

    internal static async Task<string> TokenAsync(HttpClient client, string path, CancellationToken ct)
    {
        var html = await client.GetStringAsync(path, ct);
        return WebUtility.HtmlDecode(TokenField().Match(html).Groups[1].Value);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex TokenField();
}
