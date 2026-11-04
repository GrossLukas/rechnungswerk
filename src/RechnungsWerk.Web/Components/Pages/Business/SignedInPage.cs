using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace RechnungsWerk.Web.Components.Pages.Business;

/// <summary>
/// Grundlage der Seiten mit Firmendaten. Sie laufen als eigene Live-Komponenten, an denen die Anmeldeprüfung des Routers
/// nicht mehr hängt. Endet die Anmeldung (Abmelden, Passwortwechsel), lädt die Seite deshalb neu: Mit gültigem Cookie geht
/// es dort weiter, sonst leitet der Server zur Anmeldung – speichern oder senden lässt sich im alten Tab nichts mehr.
/// </summary>
public abstract class SignedInPage : ComponentBase, IDisposable
{
    private bool subscribed;

    [Inject]
    private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        // Statische Seiten prüft der Server bei jeder Anfrage; nachhorchen müssen nur Live-Seiten.
        if (!subscribed && RendererInfo.IsInteractive)
        {
            AuthenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
            subscribed = true;
        }

        return base.SetParametersAsync(parameters);
    }

    public void Dispose()
    {
        if (subscribed)
        {
            AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        }
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> state) =>
        _ = InvokeAsync(async () =>
        {
            if ((await state).User.Identity?.IsAuthenticated != true)
            {
                NavigationManager.Refresh(forceReload: true);
            }
        });
}
