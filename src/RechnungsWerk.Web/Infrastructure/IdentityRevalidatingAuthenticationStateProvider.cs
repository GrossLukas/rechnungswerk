using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RechnungsWerk.Web.Data;

namespace RechnungsWerk.Web.Infrastructure;

/// <summary>
/// Prüft in offenen Blazor-Verbindungen den Sicherheitsstempel: regelmäßig und sofort, wenn sich jemand abmeldet oder das
/// Passwort ändert. Nach Abmelden, Passwortwechsel oder gelöschtem Konto endet die Sitzung so auch in bereits geöffneten Tabs
/// (die Seiten mit Firmendaten laden dann neu, siehe <see cref="Components.Pages.Business.SignedInPage"/>).
/// </summary>
internal sealed class IdentityRevalidatingAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IOptions<IdentityOptions> options;
    private readonly SignOutBroadcast signOuts;
    private bool listening;

    public IdentityRevalidatingAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<IdentityOptions> options,
        SignOutBroadcast signOuts)
        : base(loggerFactory)
    {
        this.scopeFactory = scopeFactory;
        this.options = options;
        this.signOuts = signOuts;
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    /// <summary>
    /// Nur für Live-Verbindungen (siehe <see cref="SignOutCircuitHandler"/>): Eine einzelne Seitenanfrage prüft der Server
    /// ohnehin, und die Anfrage, die selbst das Passwort ändert, würde sich sonst gleich mit abmelden.
    /// </summary>
    internal void ListenForSignOuts()
    {
        if (!listening)
        {
            signOuts.SignedOut += OnSignedOut;
            listening = true;
        }
    }

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        return await ValidateSecurityStampAsync(userManager, authenticationState.User);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && listening)
        {
            signOuts.SignedOut -= OnSignedOut;
        }

        base.Dispose(disposing);
    }

    private void OnSignedOut() => _ = RevalidateNowAsync();

    private async Task RevalidateNowAsync()
    {
        try
        {
            var state = await GetAuthenticationStateAsync();
            if (state.User.Identity?.IsAuthenticated == true && !await ValidateAuthenticationStateAsync(state, CancellationToken.None))
            {
                SetAuthenticationState(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
            }
        }
        catch (ObjectDisposedException)
        {
            // Die Verbindung wurde inzwischen geschlossen.
        }
    }

    private async Task<bool> ValidateSecurityStampAsync(UserManager<AppUser> userManager, ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return false;
        }

        if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }

        var principalStamp = principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
        var userStamp = await userManager.GetSecurityStampAsync(user);
        return principalStamp == userStamp;
    }
}
