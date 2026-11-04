using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace RechnungsWerk.Web.Components;

/// <summary>
/// Grundlage der interaktiven Seiten. Ersetzt eine Aktion das Bedienelement mit dem Fokus (Löschen wird zur Bestätigung,
/// die Ablage zur Ansicht, eine Position verschwindet), stünde der Fokus danach auf dem Seitenhintergrund – Tastatur- und
/// Screenreader-Nutzer verlören ihre Stelle. Die Seite nennt deshalb das Element, das danach den Fokus bekommt.
/// </summary>
public abstract class PageBase : ComponentBase
{
    private string? pendingFocus;
    private bool pendingFirstError;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    /// <summary>Setzt nach dem nächsten Zeichnen den Fokus auf das erste Element zum CSS-Selektor.</summary>
    protected void FocusAfterRender(string selector) => pendingFocus = selector;

    /// <summary>Setzt nach dem nächsten Zeichnen den Fokus auf das erste Feld mit Fehler – das rollt es auch ins Bild.</summary>
    protected void FocusFirstErrorAfterRender() => pendingFirstError = true;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var selector = pendingFocus;
        var firstError = pendingFirstError;
        pendingFocus = null;
        pendingFirstError = false;
        if (selector is null && !firstError)
        {
            return;
        }

        try
        {
            await (firstError
                ? JS.InvokeVoidAsync("rechnungsWerk.focusFirstError")
                : JS.InvokeVoidAsync("rechnungsWerk.focus", selector));
        }
        catch (JSDisconnectedException)
        {
            // Der Tab ist schon zu.
        }
    }
}
