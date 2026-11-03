using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using RechnungsWerk.Core.EInvoice;

namespace RechnungsWerk.Core.Validation;

/// <summary>Liest Prüfberichte im VARL-Format (http://www.xoev.de/de/validator/varl/1).</summary>
public static partial class ValidationReportParser
{
    private static readonly XNamespace Rep = "http://www.xoev.de/de/validator/varl/1";
    private static readonly XNamespace Scenarios = "http://www.xoev.de/de/validator/framework/1/scenarios";

    public static ValidationReport Parse(byte[] reportXml)
    {
        XDocument document;
        try
        {
            using var reader = XmlReader.Create(new MemoryStream(reportXml, writable: false), SecureXml.ReaderSettings());
            document = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new ValidatorUnavailableException("Der Prüfdienst hat einen unlesbaren Bericht geliefert.", ex);
        }

        var root = document.Root;
        if (root is null || root.Name != Rep + "report")
        {
            throw new ValidatorUnavailableException("Der Prüfdienst hat keinen Prüfbericht geliefert.");
        }

        var matched = root.Element(Rep + "scenarioMatched");
        var steps = new List<ValidationStep>();
        var messages = new List<ValidationMessage>();

        foreach (var step in (matched ?? root).Elements(Rep + "validationStepResult"))
        {
            var stepName = StepName(step);
            steps.Add(new ValidationStep(stepName, (string?)step.Attribute("valid") != "false"));

            foreach (var message in step.Elements(Rep + "message"))
            {
                var level = Level((string?)message.Attribute("level"));
                var code = Normalize((string?)message.Attribute("code"));
                messages.Add(new ValidationMessage(
                    level,
                    code,
                    MessageText(message.Value, code),
                    SimplifyLocation((string?)message.Attribute("xpathLocation")),
                    stepName,
                    RuleExplanations.Explain(code, stepName)));
            }
        }

        var accepted = root.Element(Rep + "assessment")?.Element(Rep + "accept") is not null;
        return new ValidationReport
        {
            IsAcceptable = accepted,
            ScenarioMatched = matched is not null,
            ScenarioName = matched?.Element(Scenarios + "scenario")?.Element(Scenarios + "name")?.Value.Trim(),
            EngineName = root.Element(Rep + "engine")?.Element(Rep + "name")?.Value.Trim(),
            Steps = steps,
            Messages = messages
                .OrderBy(m => m.Level)
                .ThenBy(m => m.RuleId, StringComparer.Ordinal)
                .ToList(),
        };
    }

    private static string StepName(XElement step)
    {
        var name = step.Element(Scenarios + "resource")?.Element(Scenarios + "name")?.Value.Trim();
        if (!string.IsNullOrEmpty(name))
        {
            return name;
        }

        return (string?)step.Attribute("id") switch
        {
            "val-xml" => "Wohlgeformtes XML",
            "val-xsd" => "XML-Schema",
            var id => id ?? "Prüfschritt",
        };
    }

    private static ValidationLevel Level(string? level) => level?.ToLowerInvariant() switch
    {
        "warning" => ValidationLevel.Warning,
        "information" or "info" => ValidationLevel.Information,
        _ => ValidationLevel.Error,
    };

    private static string? Normalize(string? code) => string.IsNullOrWhiteSpace(code) ? null : code.Trim();

    /// <summary>Entfernt die vorangestellte Regelkennung („[BR-DE-15]-…“), weil sie separat angezeigt wird.</summary>
    private static string MessageText(string text, string? code)
    {
        var trimmed = WhitespaceRun().Replace(text, " ").Trim();
        if (code is not null && trimmed.StartsWith($"[{code}]", StringComparison.Ordinal))
        {
            trimmed = trimmed[(code.Length + 2)..].TrimStart(' ', '-', ':').Trim();
        }

        return trimmed;
    }

    /// <summary>„/Q{urn:…}Invoice[1]/Q{urn:…}AccountingSupplierParty[1]“ → „/Invoice/AccountingSupplierParty“.</summary>
    internal static string? SimplifyLocation(string? xpath)
    {
        if (string.IsNullOrWhiteSpace(xpath))
        {
            return null;
        }

        var withoutNamespaces = QualifiedName().Replace(xpath, "");
        return FirstIndex().Replace(withoutNamespaces, "");
    }

    [GeneratedRegex(@"Q\{[^}]*\}")]
    private static partial Regex QualifiedName();

    [GeneratedRegex(@"\[1\]")]
    private static partial Regex FirstIndex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();
}
