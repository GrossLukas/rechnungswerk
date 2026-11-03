using RechnungsWerk.Core.Formatting;

namespace RechnungsWerk.Web.Infrastructure;

/// <summary>Zeitangaben werden in UTC gespeichert und in deutscher Zeit angezeigt.</summary>
public static class Display
{
    private static readonly TimeZoneInfo GermanTime = FindGermanTimeZone();

    public static DateTime ToGermanTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), GermanTime);

    public static string Timestamp(DateTime? utc) =>
        utc is null ? "" : ToGermanTime(utc.Value).ToString("dd.MM.yyyy, HH:mm 'Uhr'", GermanFormat.Culture);

    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(ToGermanTime(time.GetUtcNow().UtcDateTime));

    private static TimeZoneInfo FindGermanTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }
}

public static class LocalUrl
{
    /// <summary>Nur Pfade innerhalb dieser Anwendung zulassen – verhindert offene Weiterleitungen nach der Anmeldung.</summary>
    public static string SafeReturnUrl(string? returnUrl, string fallback = "/rechnungen")
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || !returnUrl.StartsWith('/')
            || returnUrl.StartsWith("//", StringComparison.Ordinal)
            || returnUrl.StartsWith("/\\", StringComparison.Ordinal)
            || returnUrl.Any(char.IsControl)
            || !Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        {
            return fallback;
        }

        return returnUrl;
    }
}
