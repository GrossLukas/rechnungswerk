using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace RechnungsWerk.Web.Infrastructure;

/// <summary>Schaltet für jede Live-Verbindung das sofortige Nachprüfen bei Abmeldungen und Passwortwechseln ein.</summary>
internal sealed class SignOutCircuitHandler(AuthenticationStateProvider authenticationState) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        (authenticationState as IdentityRevalidatingAuthenticationStateProvider)?.ListenForSignOuts();
        return Task.CompletedTask;
    }
}
