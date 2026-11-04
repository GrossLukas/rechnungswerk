using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RechnungsWerk.Core.EInvoice;
using RechnungsWerk.Core.Mail;
using RechnungsWerk.Web.Data;
using RechnungsWerk.Web.Services;

namespace RechnungsWerk.Tests.Web;

public sealed class InvoiceMailServiceTests
{
    [Fact]
    public async Task Sending_an_invoice_attaches_the_file_and_records_the_dispatch()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var smtp = new FakeSmtpServer();
        using var database = new TestDatabase();
        var (service, invoiceId) = await ArrangeAsync(database, smtp.Port, ct);

        var result = await service.SendInvoiceAsync(invoiceId, EInvoiceFormat.XRechnungUbl, "einkauf@musterhausen.example", ct);

        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        var message = Assert.Single(smtp.Messages);
        Assert.Contains("RCPT TO:<einkauf@musterhausen.example>", message.Envelope);
        Assert.Contains("Subject: Rechnung RE-2026-0001 von Muster Software GmbH", message.Data);
        Assert.Contains("RE-2026-0001_xrechnung.xml", message.Data);
        Assert.DoesNotContain("{Rechnungsnummer}", message.Data);

        await using var db = database.Create();
        var invoice = await db.Invoices.FindAsync([invoiceId], ct);
        Assert.NotNull(invoice!.SentAt);
        Assert.Equal("einkauf@musterhausen.example", invoice.SentTo);
        Assert.Equal(EInvoiceFormat.XRechnungUbl, invoice.SentFormat);
        Assert.Null(invoice.LastMailError);
        Assert.True(invoice.IsLocked);
    }

    [Fact]
    public async Task Failed_dispatch_keeps_the_invoice_editable_and_stores_a_readable_error()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = new TestDatabase();
        // Auf diesem Port lauscht niemand.
        var unusedPort = FakeSmtpServer.FreePort();
        var (service, invoiceId) = await ArrangeAsync(database, unusedPort, ct);

        var result = await service.SendInvoiceAsync(invoiceId, EInvoiceFormat.ZugferdPdf, "einkauf@musterhausen.example", ct);

        Assert.False(result.Succeeded);
        await using var db = database.Create();
        var invoice = await db.Invoices.FindAsync([invoiceId], ct);
        Assert.Null(invoice!.SentAt);
        Assert.False(invoice.IsLocked);
        Assert.False(string.IsNullOrWhiteSpace(invoice.LastMailError));
        Assert.NotNull(invoice.LastMailAttemptAt);
    }

    [Fact]
    public async Task Invoice_that_cannot_be_written_is_reported_and_not_sent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var smtp = new FakeSmtpServer();
        using var database = new TestDatabase();
        var (service, invoiceId) = await ArrangeAsync(database, smtp.Port, ct);
        await using (var db = database.Create())
        {
            var stored = await db.Invoices.SingleAsync(i => i.Id == invoiceId, ct);
            stored.Buyer = stored.Buyer with { CountryCode = "UK" };
            await db.SaveChangesAsync(ct);
        }

        var result = await service.SendInvoiceAsync(invoiceId, EInvoiceFormat.XRechnungUbl, "einkauf@musterhausen.example", ct);

        Assert.False(result.Succeeded);
        Assert.Contains("„UK“", Assert.Single(result.GeneralErrors), StringComparison.Ordinal);
        Assert.Empty(smtp.Messages);
    }

    [Fact]
    public async Task Invalid_recipient_is_rejected_before_anything_is_sent()
    {
        using var database = new TestDatabase();
        var (service, invoiceId) = await ArrangeAsync(database, FakeSmtpServer.FreePort(), TestContext.Current.CancellationToken);

        var result = await service.SendInvoiceAsync(invoiceId, EInvoiceFormat.XRechnungUbl, "keine-adresse", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("Empfängeradresse", result.Errors[0]);
    }

    private static async Task<(InvoiceMailService Service, int InvoiceId)> ArrangeAsync(TestDatabase database, int port, CancellationToken ct)
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero));
        await using (var db = database.Create())
        {
            var seller = TestInvoices.Seller();
            db.CompanyProfiles.Add(new CompanyProfile
            {
                Name = seller.Name,
                Street = seller.Street,
                PostalCode = seller.PostalCode,
                City = seller.City,
                VatId = seller.VatId,
                ContactName = seller.ContactName,
                Phone = seller.Phone,
                Email = seller.Email,
                Iban = seller.Iban,
            });
            db.Customers.Add(new Customer
            {
                Name = "Stadt Musterhausen",
                Street = "Rathausplatz 1",
                PostalCode = "20095",
                City = "Hamburg",
                Email = "einkauf@musterhausen.example",
            });
            await db.SaveChangesAsync(ct);
        }

        var smtpSettings = new SmtpSettingsService(database.Factory, new EphemeralDataProtectionProvider(), time);
        await smtpSettings.SaveAsync(new SmtpForm
        {
            Host = "127.0.0.1",
            Port = port,
            Security = SmtpSecurity.None,
            SenderName = "Muster Software GmbH",
            SenderEmail = "rechnung@muster.example",
        }, ct);

        var invoices = new InvoiceService(database.Factory, new EInvoiceWriter(), time);
        var saved = await invoices.SaveAsync(new InvoiceForm
        {
            CustomerId = 1,
            IssueDate = new DateOnly(2026, 11, 3),
            DeliveryDate = new DateOnly(2026, 11, 3),
            DueDate = new DateOnly(2026, 11, 17),
            Lines = [new InvoiceLineForm { Name = "Beratung", Quantity = 2, UnitCode = "HUR", UnitPrice = 95m, VatRate = 19 }],
        }, ct);
        Assert.True(saved.Succeeded, string.Join(", ", saved.Errors));

        var service = new InvoiceMailService(database.Factory, invoices, smtpSettings, new SmtpMailSender(NullLogger<SmtpMailSender>.Instance), time);
        return (service, saved.Id!.Value);
    }

    /// <summary>Minimaler SMTP-Empfänger für Tests: nimmt Mails an und merkt sie sich.</summary>
    private sealed class FakeSmtpServer : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly Task loop;

        public FakeSmtpServer()
        {
            listener.Start();
            loop = AcceptAsync();
        }

        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        public List<(string Envelope, string Data)> Messages { get; } = [];

        public static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        public async ValueTask DisposeAsync()
        {
            await stop.CancelAsync();
            listener.Stop();
            try
            {
                await loop;
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                // Beim Beenden erwartet.
            }

            stop.Dispose();
        }

        private async Task AcceptAsync()
        {
            while (!stop.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(stop.Token);
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII);
                await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
                await writer.WriteLineAsync("220 localhost ESMTP Test");
                var envelope = new StringBuilder();
                while (await reader.ReadLineAsync(stop.Token) is { } line)
                {
                    var command = line.ToUpperInvariant();
                    if (command.StartsWith("EHLO", StringComparison.Ordinal) || command.StartsWith("HELO", StringComparison.Ordinal))
                    {
                        await writer.WriteLineAsync("250-localhost");
                        await writer.WriteLineAsync("250 8BITMIME");
                    }
                    else if (command.StartsWith("MAIL FROM", StringComparison.Ordinal) || command.StartsWith("RCPT TO", StringComparison.Ordinal))
                    {
                        envelope.AppendLine(line);
                        await writer.WriteLineAsync("250 OK");
                    }
                    else if (command == "DATA")
                    {
                        await writer.WriteLineAsync("354 Go ahead");
                        var data = new StringBuilder();
                        while (await reader.ReadLineAsync(stop.Token) is { } dataLine && dataLine != ".")
                        {
                            data.AppendLine(dataLine);
                        }

                        Messages.Add((envelope.ToString(), data.ToString()));
                        await writer.WriteLineAsync("250 Queued");
                    }
                    else if (command == "QUIT")
                    {
                        await writer.WriteLineAsync("221 Bye");
                        break;
                    }
                    else
                    {
                        await writer.WriteLineAsync("250 OK");
                    }
                }
            }
        }
    }
}
