using RechnungsWerk.Core.Mail;

namespace RechnungsWerk.Tests;

public sealed class MailTemplateTests
{
    private static readonly MailTemplateValues Values = new("RE-2026-0042", "Stadt Musterhausen", "1.098,96 €", "17.11.2026", "Muster Software GmbH");

    [Fact]
    public void Replaces_all_placeholders()
    {
        var text = MailTemplate.Render("{Rechnungsnummer}|{Kunde}|{Betrag}|{Fälligkeit}|{Firma}", Values);

        Assert.Equal("RE-2026-0042|Stadt Musterhausen|1.098,96 €|17.11.2026|Muster Software GmbH", text);
    }

    [Fact]
    public void Placeholders_ignore_case_and_accept_spelling_without_umlaut()
    {
        Assert.Equal("17.11.2026 / RE-2026-0042", MailTemplate.Render("{faelligkeit} / {RECHNUNGSNUMMER}", Values));
    }

    [Fact]
    public void Unknown_placeholders_and_plain_braces_stay_untouched()
    {
        Assert.Equal("{Unbekannt} {} {Kunde 2}", MailTemplate.Render("{Unbekannt} {} {Kunde 2}", Values));
    }

    [Fact]
    public void Subject_cannot_contain_line_breaks()
    {
        var values = Values with { CustomerName = "Böse\r\nBcc: angreifer@example.com" };

        var subject = MailTemplate.RenderSubject("Rechnung für {Kunde}", values);

        Assert.DoesNotContain('\n', subject);
        Assert.DoesNotContain('\r', subject);
    }

    [Fact]
    public void Default_template_mentions_number_amount_and_due_date()
    {
        var body = MailTemplate.Render(MailTemplate.DefaultBody, Values);

        Assert.Contains("RE-2026-0042", body, StringComparison.Ordinal);
        Assert.Contains("1.098,96 €", body, StringComparison.Ordinal);
        Assert.Contains("17.11.2026", body, StringComparison.Ordinal);
        Assert.DoesNotContain("{", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_template_without_due_date_asks_for_payment_right_away()
    {
        var body = MailTemplate.Render(MailTemplate.DefaultBody, Values with { DueDate = null });

        Assert.Contains("Bitte überweisen Sie den Betrag sofort.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("bis zum", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zahlbar bis {faelligkeit}", "zahlbar sofort")]
    [InlineData("Fällig: {Fälligkeit}", "Fällig: sofort")]
    public void Due_date_placeholder_without_due_date_reads_sofort(string template, string expected)
    {
        Assert.Equal(expected, MailTemplate.Render(template, Values with { DueDate = null }));
    }
}
