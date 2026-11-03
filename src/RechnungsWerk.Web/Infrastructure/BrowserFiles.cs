using Microsoft.AspNetCore.Components.Forms;
using RechnungsWerk.Core.EInvoice;

namespace RechnungsWerk.Web.Infrastructure;

public static class BrowserFiles
{
    /// <summary>Liest eine hochgeladene Datei vollständig in den Speicher – sie wird nirgends abgelegt.</summary>
    public static async Task<byte[]> ReadAsync(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        if (file.Size > EInvoiceReader.MaxFileBytes)
        {
            throw new EInvoiceReadException("Die Datei ist größer als 10 MB. Bitte wähle eine kleinere Datei.");
        }

        if (file.Size == 0)
        {
            throw new EInvoiceReadException("Die Datei ist leer.");
        }

        try
        {
            await using var stream = file.OpenReadStream(EInvoiceReader.MaxFileBytes, cancellationToken);
            using var buffer = new MemoryStream((int)file.Size);
            await stream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
        catch (IOException ex)
        {
            throw new EInvoiceReadException("Die Datei konnte nicht vollständig hochgeladen werden. Bitte versuche es noch einmal.", ex);
        }
    }
}
