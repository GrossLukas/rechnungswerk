using Microsoft.Extensions.Logging.Abstractions;
using RechnungsWerk.Core.EInvoice;
using RechnungsWerk.Core.Validation;

namespace RechnungsWerk.Tests;

/// <summary>
/// Prüft die erzeugten Dateien mit dem echten KoSIT-Validator. Läuft nur, wenn RECHNUNGSWERK_VALIDATOR_URL gesetzt ist,
/// z. B. auf http://127.0.0.1:8081/ (Validator im Daemon-Modus, siehe README).
/// </summary>
[Trait("Category", "Validator")]
public sealed class KositValidatorIntegrationTests
{
    private static readonly string? ValidatorUrl = Environment.GetEnvironmentVariable("RECHNUNGSWERK_VALIDATOR_URL");

    [Theory]
    [MemberData(nameof(EInvoiceRoundTripTests.AllFormats), MemberType = typeof(EInvoiceRoundTripTests))]
    public async Task Generated_invoice_is_accepted_by_the_kosit_validator(EInvoiceFormat format, bool smallBusiness)
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ValidatorUrl), "RECHNUNGSWERK_VALIDATOR_URL ist nicht gesetzt.");

        var file = new EInvoiceWriter().Write(TestInvoices.Sample(smallBusiness), format);
        var xml = new EInvoiceReader().ExtractXml(file);
        var client = new KositValidatorClient(new HttpClient(), new ValidatorOptions { Url = ValidatorUrl, TimeoutSeconds = 120 },
            NullLogger<KositValidatorClient>.Instance);

        var report = await client.ValidateAsync(xml, TestContext.Current.CancellationToken);

        var problems = string.Join(Environment.NewLine, report.Messages.Select(m => $"{m.Level} {m.RuleId}: {m.Text}"));
        Assert.True(report.IsAcceptable, $"{format} (Kleinunternehmer: {smallBusiness}) abgelehnt:{Environment.NewLine}{problems}");
        Assert.Equal(0, report.ErrorCount);
        Assert.Equal(format == EInvoiceFormat.ZugferdPdf ? "EN16931 (CII)" : format == EInvoiceFormat.XRechnungUbl
            ? "EN16931 XRechnung (UBL Invoice)" : "EN16931 XRechnung (CII)", report.ScenarioName);
    }
}
