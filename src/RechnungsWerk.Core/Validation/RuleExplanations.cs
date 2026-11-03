namespace RechnungsWerk.Core.Validation;

/// <summary>Kurze deutsche Erklärungen zu häufigen Regeln aus EN 16931, XRechnung und Peppol.</summary>
public static class RuleExplanations
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BR-01"] = "Die Spezifikationskennung (BT-24) fehlt – sie sagt, nach welchem Standard die Rechnung aufgebaut ist.",
        ["BR-02"] = "Die Rechnungsnummer (BT-1) fehlt.",
        ["BR-03"] = "Das Rechnungsdatum (BT-2) fehlt.",
        ["BR-04"] = "Der Rechnungstyp (BT-3), z. B. 380 für Rechnung, fehlt.",
        ["BR-05"] = "Die Rechnungswährung (BT-5) fehlt.",
        ["BR-06"] = "Der Name des Rechnungsstellers (BT-27) fehlt.",
        ["BR-07"] = "Der Name des Rechnungsempfängers (BT-44) fehlt.",
        ["BR-08"] = "Die Anschrift des Rechnungsstellers (BG-5) fehlt.",
        ["BR-09"] = "Der Ländercode des Rechnungsstellers (BT-40) fehlt.",
        ["BR-10"] = "Die Anschrift des Rechnungsempfängers (BG-8) fehlt.",
        ["BR-11"] = "Der Ländercode des Rechnungsempfängers (BT-55) fehlt.",
        ["BR-12"] = "Die Summe der Positionsbeträge (BT-106) fehlt.",
        ["BR-13"] = "Der Gesamtbetrag ohne Umsatzsteuer (BT-109) fehlt.",
        ["BR-14"] = "Der Gesamtbetrag mit Umsatzsteuer (BT-112) fehlt.",
        ["BR-15"] = "Der zu zahlende Betrag (BT-115) fehlt.",
        ["BR-16"] = "Die Rechnung enthält keine einzige Position.",
        ["BR-21"] = "Eine Position hat keine Positionsnummer (BT-126).",
        ["BR-22"] = "Bei einer Position fehlt die Menge (BT-129).",
        ["BR-23"] = "Bei einer Position fehlt die Mengeneinheit (BT-130).",
        ["BR-24"] = "Bei einer Position fehlt der Nettobetrag (BT-131).",
        ["BR-25"] = "Bei einer Position fehlt die Bezeichnung (BT-153).",
        ["BR-26"] = "Bei einer Position fehlt der Nettopreis (BT-146).",
        ["BR-27"] = "Ein Nettopreis ist negativ. Rabatte gehören als Nachlass oder als Position mit negativer Menge in die Rechnung.",
        ["BR-28"] = "Ein Bruttopreis ist negativ.",
        ["BR-29"] = "Beim Leistungszeitraum der Rechnung liegt das Ende vor dem Beginn.",
        ["BR-30"] = "Beim Leistungszeitraum einer Position liegt das Ende vor dem Beginn.",
        ["BR-CO-03"] = "Steuerdatum und Code für das Steuerfälligkeitsdatum dürfen nicht beide angegeben sein.",
        ["BR-CO-04"] = "Jede Position braucht eine Umsatzsteuerkategorie (BT-151).",
        ["BR-CO-09"] = "Die USt-IdNr. muss mit dem Ländercode beginnen, z. B. DE123456789.",
        ["BR-CO-10"] = "Die Summe der Positionsbeträge (BT-106) stimmt nicht mit den einzelnen Positionen überein.",
        ["BR-CO-11"] = "Die Summe der Nachlässe (BT-107) stimmt nicht mit den einzelnen Nachlässen überein.",
        ["BR-CO-12"] = "Die Summe der Zuschläge (BT-108) stimmt nicht mit den einzelnen Zuschlägen überein.",
        ["BR-CO-13"] = "Der Nettobetrag (BT-109) passt nicht zu Positionen, Nachlässen und Zuschlägen.",
        ["BR-CO-14"] = "Die Umsatzsteuer gesamt (BT-110) entspricht nicht der Summe der Steuerbeträge je Steuersatz.",
        ["BR-CO-15"] = "Der Bruttobetrag (BT-112) ist nicht Nettobetrag plus Umsatzsteuer.",
        ["BR-CO-16"] = "Der Zahlbetrag (BT-115) passt nicht zu Bruttobetrag, Anzahlungen und Rundung.",
        ["BR-CO-17"] = "Ein Steuerbetrag ist falsch berechnet: Er muss Bemessungsgrundlage × Steuersatz sein, auf Cent gerundet.",
        ["BR-CO-18"] = "Die Rechnung braucht mindestens eine Aufschlüsselung der Umsatzsteuer (BG-23).",
        ["BR-CO-19"] = "Beim Leistungszeitraum muss mindestens Beginn oder Ende angegeben sein.",
        ["BR-CO-25"] = "Bei einem positiven Zahlbetrag muss ein Fälligkeitsdatum (BT-9) oder eine Zahlungsbedingung (BT-20) angegeben sein.",
        ["BR-CO-26"] = "Für den Rechnungssteller fehlt eine Kennung: USt-IdNr., Registernummer oder eigene Kennung.",
        ["BR-S-02"] = "Bei Positionen mit Regelsteuersatz braucht der Rechnungssteller eine USt-IdNr. oder Steuernummer.",
        ["BR-S-05"] = "Bei Positionen mit Regelsteuersatz muss ein Steuersatz über 0 % angegeben sein.",
        ["BR-S-08"] = "Die Bemessungsgrundlage für einen Steuersatz passt nicht zur Summe der Positionen mit diesem Satz.",
        ["BR-S-09"] = "Der Steuerbetrag für einen Steuersatz ist falsch berechnet.",
        ["BR-E-01"] = "Bei steuerbefreiten Positionen muss es genau eine Steueraufschlüsselung „steuerbefreit“ geben.",
        ["BR-E-05"] = "Steuerbefreite Positionen müssen den Steuersatz 0 % haben.",
        ["BR-E-08"] = "Die Bemessungsgrundlage „steuerbefreit“ passt nicht zur Summe der befreiten Positionen.",
        ["BR-E-09"] = "Bei steuerbefreiten Umsätzen muss der Steuerbetrag 0 sein.",
        ["BR-E-10"] = "Für die Steuerbefreiung fehlt der Grund (Text oder Code), z. B. „Kleinunternehmer gemäß § 19 UStG“.",
        ["BR-AE-02"] = "Reverse Charge: Die USt-IdNr. des Rechnungsempfängers fehlt.",
        ["BR-AE-10"] = "Reverse Charge: Der Hinweis „Steuerschuldnerschaft des Leistungsempfängers“ fehlt.",
        ["BR-DE-1"] = "XRechnung verlangt Zahlungsanweisungen (BG-16), z. B. Überweisung mit IBAN.",
        ["BR-DE-2"] = "XRechnung verlangt Kontaktangaben des Rechnungsstellers (BG-6).",
        ["BR-DE-3"] = "Der Ort des Rechnungsstellers (BT-37) fehlt.",
        ["BR-DE-4"] = "Die Postleitzahl des Rechnungsstellers (BT-38) fehlt.",
        ["BR-DE-5"] = "Der Name der Kontaktperson beim Rechnungssteller (BT-41) fehlt.",
        ["BR-DE-6"] = "Die Telefonnummer der Kontaktperson (BT-42) fehlt.",
        ["BR-DE-7"] = "Die E-Mail-Adresse der Kontaktperson (BT-43) fehlt.",
        ["BR-DE-8"] = "Der Ort des Rechnungsempfängers (BT-52) fehlt.",
        ["BR-DE-9"] = "Die Postleitzahl des Rechnungsempfängers (BT-53) fehlt.",
        ["BR-DE-10"] = "Bei der Lieferanschrift fehlt der Ort.",
        ["BR-DE-11"] = "Bei der Lieferanschrift fehlt die Postleitzahl.",
        ["BR-DE-14"] = "Der Steuersatz (BT-119) fehlt in einer Steueraufschlüsselung.",
        ["BR-DE-15"] = "Die Käuferreferenz (BT-10) fehlt. Bei Behörden ist das die Leitweg-ID.",
        ["BR-DE-16"] = "Die USt-IdNr. oder Steuernummer des Rechnungsstellers fehlt.",
        ["BR-DE-17"] = "Dieser Rechnungstyp ist in XRechnung nicht zulässig.",
        ["BR-DE-18"] = "Skonto-Angaben in den Zahlungsbedingungen haben nicht das vorgeschriebene Format (#SKONTO#TAGE=…#PROZENT=…#).",
        ["BR-DE-19"] = "Die IBAN ist ungültig (Prüfziffer oder Länge stimmt nicht).",
        ["BR-DE-20"] = "Die IBAN des Lastschriftkontos ist ungültig.",
        ["BR-DE-21"] = "Die Spezifikationskennung (BT-24) gehört nicht zu XRechnung 3.0.",
        ["BR-DE-22"] = "Die Dateinamen der Anhänge müssen eindeutig sein.",
        ["BR-DE-23-a"] = "Bei Überweisung muss eine Kontoverbindung mit IBAN (BG-17) angegeben sein.",
        ["BR-DE-23-b"] = "Bei Überweisung dürfen keine Karten- oder Lastschriftdaten angegeben sein.",
        ["BR-DE-24-a"] = "Bei Kartenzahlung fehlen die Kartendaten (BG-18).",
        ["BR-DE-24-b"] = "Bei Kartenzahlung dürfen keine Überweisungs- oder Lastschriftdaten angegeben sein.",
        ["BR-DE-25-a"] = "Bei Lastschrift fehlen die Mandatsangaben (BG-19).",
        ["BR-DE-25-b"] = "Bei Lastschrift dürfen keine Überweisungs- oder Kartendaten angegeben sein.",
        ["BR-DE-26"] = "Bei einer korrigierten Rechnung fehlt der Verweis auf die ursprüngliche Rechnung.",
        ["BR-DE-27"] = "Die Telefonnummer sollte mindestens drei Ziffern enthalten.",
        ["BR-DE-28"] = "Die E-Mail-Adresse hat kein gültiges Format.",
        ["BR-DE-30"] = "Bei Lastschrift fehlt die Gläubiger-Identifikationsnummer.",
        ["BR-DE-31"] = "Bei Lastschrift fehlt die IBAN des belasteten Kontos.",
        ["PEPPOL-EN16931-R001"] = "Der Geschäftsprozess (BT-23) fehlt.",
        ["PEPPOL-EN16931-R010"] = "Die elektronische Adresse des Rechnungsempfängers (BT-49) fehlt, z. B. Leitweg-ID oder E-Mail.",
        ["PEPPOL-EN16931-R020"] = "Die elektronische Adresse des Rechnungsstellers (BT-34) fehlt, z. B. eine E-Mail-Adresse.",
    };

    public static string Explain(string? ruleId, string? step = null)
    {
        if (ruleId is not null && Known.TryGetValue(ruleId, out var known))
        {
            return known;
        }

        if (ruleId is null)
        {
            return step?.Contains("Schema", StringComparison.OrdinalIgnoreCase) == true
                ? "Aufbaufehler: Die Datei entspricht nicht dem XML-Schema. Ein Element fehlt, ist unbekannt oder steht an der falschen Stelle."
                : "Allgemeiner Hinweis des Prüfdienstes.";
        }

        return ruleId.ToUpperInvariant() switch
        {
            var id when id.StartsWith("BR-DEX-", StringComparison.Ordinal) => "Regel der XRechnung-Erweiterung (Extension).",
            var id when id.StartsWith("BR-DE-", StringComparison.Ordinal) => "Deutsche Zusatzregel der XRechnung.",
            var id when id.StartsWith("BR-CO-", StringComparison.Ordinal) => "Rechen- oder Abhängigkeitsregel der EN 16931.",
            var id when id.StartsWith("BR-DEC-", StringComparison.Ordinal) => "Ein Betrag hat zu viele Nachkommastellen (erlaubt sind höchstens zwei).",
            var id when id.StartsWith("BR-CL-", StringComparison.Ordinal) => "Ein Wert stammt nicht aus der vorgeschriebenen Codeliste (z. B. Land, Währung oder Einheit).",
            var id when id.StartsWith("BR-S-", StringComparison.Ordinal) => "Regel zum Regelsteuersatz (Steuerkategorie S).",
            var id when id.StartsWith("BR-Z-", StringComparison.Ordinal) => "Regel zum Nullsatz (Steuerkategorie Z).",
            var id when id.StartsWith("BR-E-", StringComparison.Ordinal) => "Regel zu steuerbefreiten Umsätzen (Steuerkategorie E).",
            var id when id.StartsWith("BR-AE-", StringComparison.Ordinal) => "Regel zur Steuerschuldnerschaft des Leistungsempfängers (Reverse Charge).",
            var id when id.StartsWith("BR-IC-", StringComparison.Ordinal) || id.StartsWith("BR-K-", StringComparison.Ordinal) => "Regel zu innergemeinschaftlichen Lieferungen.",
            var id when id.StartsWith("BR-G-", StringComparison.Ordinal) => "Regel zu steuerfreien Ausfuhren.",
            var id when id.StartsWith("BR-O-", StringComparison.Ordinal) => "Regel zu nicht steuerbaren Umsätzen.",
            var id when id.StartsWith("BR-", StringComparison.Ordinal) => "Grundregel der EN 16931 (Pflichtangabe oder Plausibilität).",
            var id when id.StartsWith("PEPPOL-", StringComparison.Ordinal) => "Regel aus dem Peppol-Regelwerk, die XRechnung übernimmt.",
            var id when id.Contains("-SR-", StringComparison.Ordinal) =>
                "Syntaxregel: Ein Element ist doppelt, an der falschen Stelle oder in dieser Syntax nicht vorgesehen.",
            var id when id.Contains("-CR-", StringComparison.Ordinal) => "Hinweis: Dieses Element wird von der EN 16931 nicht verwendet.",
            _ => "Regel des Prüfdienstes.",
        };
    }
}
