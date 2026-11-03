using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace RechnungsWerk.Web.Components;

public static class AppRenderModes
{
    /// <summary>
    /// Für Seiten, die Daten laden: erst in der Live-Verbindung zeichnen. Ohne Vorab-HTML gibt es kein Flackern
    /// zwischen zwei Ladevorgängen und keine doppelte Datenbankabfrage.
    /// </summary>
    public static readonly IComponentRenderMode Live = new InteractiveServerRenderMode(prerender: false);
}
