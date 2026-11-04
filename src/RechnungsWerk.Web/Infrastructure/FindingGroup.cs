using RechnungsWerk.Core.Validation;

namespace RechnungsWerk.Web.Infrastructure;

/// <summary>Funde eines Prüfberichts mit derselben Schwere – im Bericht unter einer Überschrift wie „3 Fehler“.</summary>
public sealed record FindingGroup(ValidationLevel Level, IReadOnlyList<ValidationMessage> Messages)
{
    /// <summary>Die Schwere steht im Text und nicht nur in der Farbe des Randes.</summary>
    public string Title => TitleFor(Level, Messages.Count);

    /// <summary>Erst Fehler, dann Warnungen, dann Hinweise; leere Gruppen gibt es nicht.</summary>
    public static IReadOnlyList<FindingGroup> Of(ValidationReport report) =>
        report.Messages.GroupBy(m => m.Level).OrderBy(g => g.Key).Select(g => new FindingGroup(g.Key, g.ToList())).ToList();

    public static string TitleFor(ValidationLevel level, int count) => level switch
    {
        ValidationLevel.Error => $"{count} Fehler",
        ValidationLevel.Warning => count == 1 ? "1 Warnung" : $"{count} Warnungen",
        _ => count == 1 ? "1 Hinweis" : $"{count} Hinweise",
    };
}
