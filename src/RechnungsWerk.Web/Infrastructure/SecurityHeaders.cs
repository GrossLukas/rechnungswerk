using Microsoft.Net.Http.Headers;

namespace RechnungsWerk.Web.Infrastructure;

public static class SecurityHeaders
{
    /// <summary>
    /// Nur eigene Skripte und Stile, keine Einbettung in fremde Seiten. Blazor Server kommt ohne Inline-Skripte aus;
    /// 'self' deckt nach CSP Level 3 auch die WebSocket-Verbindung zum eigenen Host ab.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; " +
        "connect-src 'self'; frame-ancestors 'none'; form-action 'self'; base-uri 'self'; object-src 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.OnStarting(static state =>
            {
                var headers = ((HttpContext)state).Response.Headers;
                headers[HeaderNames.XContentTypeOptions] = "nosniff";
                headers[HeaderNames.XFrameOptions] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                headers[HeaderNames.ContentSecurityPolicy] = ContentSecurityPolicy;

                // Seiten mit Firmendaten sollen nicht im Browser-Cache landen; statische Dateien setzen eigene Regeln.
                if (!headers.ContainsKey(HeaderNames.CacheControl))
                {
                    headers[HeaderNames.CacheControl] = "no-store";
                }

                return Task.CompletedTask;
            }, context);

            return next(context);
        });
}
