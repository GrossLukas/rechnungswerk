namespace RechnungsWerk.Web.Infrastructure;

/// <summary>
/// Meldet allen offenen Blazor-Verbindungen, dass Anmeldungen ungültig geworden sein können (Abmelden, Passwortwechsel).
/// Sie prüfen dann sofort ihren Sicherheitsstempel, statt bis zum nächsten Prüfintervall weiterzuarbeiten.
/// </summary>
public sealed class SignOutBroadcast
{
    public event Action? SignedOut;

    public void Announce() => SignedOut?.Invoke();
}
