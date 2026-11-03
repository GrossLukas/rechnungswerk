using System.Text;
using RechnungsWerk.Core.Validation;

namespace RechnungsWerk.Tests;

public sealed class ValidationReportParserTests
{
    private const string RejectedReport = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rep:report xmlns:rep="http://www.xoev.de/de/validator/varl/1" xmlns:s="http://www.xoev.de/de/validator/framework/1/scenarios" valid="false">
          <rep:engine><rep:name>KoSIT Validator 1.6.3</rep:name></rep:engine>
          <rep:scenarioMatched>
            <s:scenario><s:name>EN16931 XRechnung (UBL Invoice)</s:name></s:scenario>
            <rep:validationStepResult id="val-xsd" valid="true">
              <s:resource><s:name>XML Schema for UBL 2.1 Invoice</s:name></s:resource>
            </rep:validationStepResult>
            <rep:validationStepResult id="val-sch.2" valid="false">
              <s:resource><s:name>Schematron rules for Invoice - CIUS XRechnung (UBL)</s:name></s:resource>
              <rep:message id="m1" level="warning" code="BR-DE-27" xpathLocation="/Q{urn:oasis:names:specification:ubl:schema:xsd:Invoice-2}Invoice[1]/Q{x}Contact[1]">[BR-DE-27] Telefonnummer zu kurz.</rep:message>
              <rep:message id="m2" level="error" code="BR-DE-15" xpathLocation="/Q{urn:oasis:names:specification:ubl:schema:xsd:Invoice-2}Invoice[1]">[BR-DE-15] Das Element "Buyer reference" (BT-10) muss übermittelt werden.</rep:message>
              <rep:message id="m3" level="error" code="BR-ZZ-99">[BR-ZZ-99]-Unbekannte Regel</rep:message>
            </rep:validationStepResult>
          </rep:scenarioMatched>
          <rep:assessment><rep:reject><rep:explanation><html xmlns="http://www.w3.org/1999/xhtml"><body>abgelehnt</body></html></rep:explanation></rep:reject></rep:assessment>
        </rep:report>
        """;

    [Fact]
    public void Parses_rejection_with_errors_first_and_german_explanations()
    {
        var report = ValidationReportParser.Parse(Encoding.UTF8.GetBytes(RejectedReport));

        Assert.False(report.IsAcceptable);
        Assert.True(report.ScenarioMatched);
        Assert.Equal("EN16931 XRechnung (UBL Invoice)", report.ScenarioName);
        Assert.Equal("KoSIT Validator 1.6.3", report.EngineName);
        Assert.Equal(2, report.ErrorCount);
        Assert.Equal(1, report.WarningCount);
        Assert.Equal([true, false], report.Steps.Select(s => s.Valid));

        var first = report.Messages[0];
        Assert.Equal(ValidationLevel.Error, first.Level);
        Assert.Equal("BR-DE-15", first.RuleId);
        Assert.Equal("Das Element \"Buyer reference\" (BT-10) muss übermittelt werden.", first.Text);
        Assert.Equal("/Invoice", first.Location);
        Assert.Contains("Leitweg-ID", first.Explanation, StringComparison.Ordinal);

        var unknown = Assert.Single(report.Messages, m => m.RuleId == "BR-ZZ-99");
        Assert.Equal("Unbekannte Regel", unknown.Text);
        Assert.Contains("EN 16931", unknown.Explanation, StringComparison.Ordinal);
        Assert.Equal(ValidationLevel.Warning, report.Messages[^1].Level);
    }

    [Fact]
    public void Parses_acceptance()
    {
        var accepted = RejectedReport
            .Replace("<rep:reject>", "<rep:accept>", StringComparison.Ordinal)
            .Replace("</rep:reject>", "</rep:accept>", StringComparison.Ordinal);

        Assert.True(ValidationReportParser.Parse(Encoding.UTF8.GetBytes(accepted)).IsAcceptable);
    }

    [Fact]
    public void Report_without_matching_scenario_is_not_acceptable()
    {
        const string report = """
            <rep:report xmlns:rep="http://www.xoev.de/de/validator/varl/1" valid="false">
              <rep:noScenarioMatched/>
              <rep:assessment><rep:reject/></rep:assessment>
            </rep:report>
            """;

        var parsed = ValidationReportParser.Parse(Encoding.UTF8.GetBytes(report));

        Assert.False(parsed.IsAcceptable);
        Assert.False(parsed.ScenarioMatched);
    }

    [Fact]
    public void Garbage_is_reported_as_unavailable_validator()
    {
        Assert.Throws<ValidatorUnavailableException>(() => ValidationReportParser.Parse(Encoding.UTF8.GetBytes("<html>Proxy-Fehler</html>")));
    }

    [Fact]
    public async Task Unreachable_validator_gives_a_friendly_message()
    {
        var client = new KositValidatorClient(new HttpClient(), new ValidatorOptions { Url = "http://127.0.0.1:9/", TimeoutSeconds = 5 },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<KositValidatorClient>.Instance);

        var error = await Assert.ThrowsAsync<ValidatorUnavailableException>(() => client.ValidateAsync("<x/>"u8.ToArray(), TestContext.Current.CancellationToken));

        Assert.Contains("nicht", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_configuration_is_explained()
    {
        var client = new KositValidatorClient(new HttpClient(), new ValidatorOptions(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<KositValidatorClient>.Instance);

        Assert.False(client.IsConfigured);
        var error = await Assert.ThrowsAsync<ValidatorUnavailableException>(() => client.ValidateAsync("<x/>"u8.ToArray(), TestContext.Current.CancellationToken));
        Assert.Contains("Validator:Url", error.Message, StringComparison.Ordinal);
    }
}
