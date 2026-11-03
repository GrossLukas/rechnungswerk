using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using RechnungsWerk.Core.EInvoice;
using RechnungsWerk.Web.Data;
using RechnungsWerk.Web.Services;

namespace RechnungsWerk.Web.Endpoints;

public static class AppEndpoints
{
    public static IEndpointRouteBuilder MapAppEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Abmelden nur per POST mit Antiforgery-Token (das Formular-Feld sorgt für die Prüfung).
        endpoints.MapPost("/abmelden", async ([FromForm] string? returnUrl, SignInManager<AppUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return TypedResults.LocalRedirect("/anmelden");
        });

        endpoints.MapGet("/rechnungen/{id:int}/datei/{format}", async (int id, string format, InvoiceService invoices, HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (!EInvoiceFormats.TryParse(format, out var parsed))
            {
                return Results.NotFound();
            }

            var file = await invoices.RenderAsync(id, parsed, cancellationToken);
            if (file is null)
            {
                return Results.NotFound();
            }

            context.Response.Headers[HeaderNames.CacheControl] = "no-store";
            return Results.File(file.Content, file.ContentType, file.FileName);
        }).RequireAuthorization();

        return endpoints;
    }
}
