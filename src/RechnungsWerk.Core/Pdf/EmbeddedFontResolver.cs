using System.Collections.Concurrent;
using PdfSharp.Fonts;

namespace RechnungsWerk.Core.Pdf;

/// <summary>
/// Liefert die mitgelieferte Schrift Liberation Sans (SIL OFL 1.1). Container-Images haben meist keine
/// Systemschriften, und PDF/A verlangt ohnehin vollständig eingebettete Schriften.
/// </summary>
internal sealed class EmbeddedFontResolver : IFontResolver
{
    public const string FamilyName = "Liberation Sans";

    private const string RegularFace = "LiberationSans-Regular";
    private const string BoldFace = "LiberationSans-Bold";

    private static readonly Lock Gate = new();
    private static readonly ConcurrentDictionary<string, byte[]> Cache = new(StringComparer.Ordinal);
    private static bool registered;

    public static void EnsureRegistered()
    {
        lock (Gate)
        {
            if (registered)
            {
                return;
            }

            GlobalFontSettings.FontResolver ??= new EmbeddedFontResolver();
            registered = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? BoldFace : RegularFace, false, isItalic);

    public byte[]? GetFont(string faceName) =>
        Cache.GetOrAdd(faceName, static name =>
        {
            using var stream = typeof(EmbeddedFontResolver).Assembly.GetManifestResourceStream($"RechnungsWerk.Fonts.{name}.ttf")
                ?? throw new InvalidOperationException($"Schriftdatei {name} fehlt in der Assembly.");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        });
}
