namespace RechnungsWerk.Core.Invoicing;

/// <summary>Häufige Einheiten nach UN/ECE Recommendation 20 mit deutscher Bezeichnung.</summary>
public static class UnitCodes
{
    public static readonly IReadOnlyList<(string Code, string Label)> Common =
    [
        ("C62", "Stück"),
        ("HUR", "Stunde"),
        ("MIN", "Minute"),
        ("DAY", "Tag"),
        ("WEE", "Woche"),
        ("MON", "Monat"),
        ("ANN", "Jahr"),
        ("LS", "Pauschale"),
        ("SET", "Satz"),
        ("E48", "Serviceeinheit"),
        ("KGM", "Kilogramm"),
        ("TNE", "Tonne"),
        ("MTR", "Meter"),
        ("KMT", "Kilometer"),
        ("MTK", "Quadratmeter"),
        ("MTQ", "Kubikmeter"),
        ("LTR", "Liter"),
        ("P1", "Prozent"),
        ("H87", "Stück (Teil)"),
    ];

    private static readonly Dictionary<string, string> Labels =
        Common.ToDictionary(u => u.Code, u => u.Label, StringComparer.OrdinalIgnoreCase);

    public static bool IsKnown(string? code) => code is not null && Labels.ContainsKey(code);

    /// <summary>Deutsche Bezeichnung oder der Code selbst, wenn er nicht in der Liste steht.</summary>
    public static string Label(string? code) =>
        code is null ? "" : Labels.TryGetValue(code, out var label) ? label : code;
}

/// <summary>Bezeichnungen für Codelisten, die in empfangenen Rechnungen vorkommen.</summary>
public static class CodeLabels
{
    public static string TaxCategory(string? code) => code switch
    {
        "S" => "Regelsteuersatz",
        "Z" => "Nullsatz",
        "E" => "Steuerbefreit",
        "AE" => "Steuerschuldnerschaft des Leistungsempfängers",
        "K" => "Innergemeinschaftliche Lieferung",
        "G" => "Ausfuhr (steuerfrei)",
        "O" => "Nicht steuerbar",
        "L" => "Kanarische Inseln (IGIC)",
        "M" => "Ceuta und Melilla (IPSI)",
        null or "" => "",
        _ => code,
    };

    public static string InvoiceType(string? code) => code switch
    {
        "380" => "Rechnung",
        "381" => "Gutschrift",
        "384" => "Korrigierte Rechnung",
        "389" => "Selbst ausgestellte Rechnung (Gutschriftverfahren)",
        "326" => "Teilrechnung",
        "386" => "Vorauszahlungsrechnung",
        "875" => "Abschlagsrechnung (Bauleistung)",
        "876" => "Teilschlussrechnung (Bauleistung)",
        "877" => "Schlussrechnung (Bauleistung)",
        "261" => "Selbst ausgestellte Gutschrift",
        "383" => "Belastungsanzeige",
        null or "" => "Rechnung",
        _ => $"Rechnung (Typ {code})",
    };

    public static string PaymentMeans(string? code) => code switch
    {
        "1" => "Nicht festgelegt",
        "10" => "Barzahlung",
        "20" => "Scheck",
        "30" => "Überweisung",
        "31" => "Überweisung",
        "42" => "Zahlung auf Bankkonto",
        "48" => "Kartenzahlung",
        "49" => "Lastschrift",
        "54" => "Kreditkarte",
        "55" => "Debitkarte",
        "57" => "Dauerauftrag",
        "58" => "SEPA-Überweisung",
        "59" => "SEPA-Lastschrift",
        "68" => "Online-Zahlungsdienst",
        "97" => "Verrechnung",
        null or "" => "",
        _ => $"Zahlungsart {code}",
    };

    public static string Country(string? code) => code switch
    {
        "DE" => "Deutschland",
        "AT" => "Österreich",
        "CH" => "Schweiz",
        "FR" => "Frankreich",
        "NL" => "Niederlande",
        "BE" => "Belgien",
        "LU" => "Luxemburg",
        "IT" => "Italien",
        "ES" => "Spanien",
        "PL" => "Polen",
        "CZ" => "Tschechien",
        "DK" => "Dänemark",
        null or "" => "",
        _ => code,
    };
}
