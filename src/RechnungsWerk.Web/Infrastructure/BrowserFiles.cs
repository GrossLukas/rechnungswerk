using Microsoft.AspNetCore.Components.Forms;
using RechnungsWerk.Core.EInvoice;

namespace RechnungsWerk.Web.Infrastructure;

public static class BrowserFiles
{
    // Anonyme Uploads bis 10 MB: mehr als vier gleichzeitig würden nur den Arbeitsspeicher füllen.
    private static readonly SemaphoreSlim UploadGate = new(4, 4);

    /// <summary>Begrenzt, wie viele hochgeladene Dateien gleichzeitig gelesen und verarbeitet werden.</summary>
    public static async Task<T> ProcessAsync<T>(IBrowserFile file, Func<byte[], Task<T>> process, CancellationToken cancellationToken = default)
    {
        if (!await UploadGate.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken))
        {
            throw new EInvoiceReadException("Gerade werden viele Dateien gleichzeitig verarbeitet. Bitte versuche es gleich noch einmal.");
        }

        try
        {
            return await process(await ReadAsync(file, cancellationToken));
        }
        finally
        {
            UploadGate.Release();
        }
    }

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
