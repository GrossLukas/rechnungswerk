using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace RechnungsWerk.Core.Validation;

public sealed class ValidatorOptions
{
    public const string SectionName = "Validator";

    /// <summary>Adresse des KoSIT-Validators im Daemon-Modus, z. B. http://validator:8081/.</summary>
    public string? Url { get; set; }

    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// Spricht den KoSIT-Validator im Daemon-Modus an: XML per POST, Antwort ist der Prüfbericht.
/// HTTP 200 = annehmbar, 406 = nicht annehmbar, 422 = Verarbeitungsfehler.
/// </summary>
public sealed class KositValidatorClient(HttpClient http, ValidatorOptions options, ILogger<KositValidatorClient> logger)
{
    // Der Validator ist rechenintensiv; mehr als vier gleichzeitige Prüfungen bremsen nur alle aus.
    private static readonly SemaphoreSlim Gate = new(4, 4);

    public bool IsConfigured => Uri.TryCreate(options.Url, UriKind.Absolute, out _);

    public async Task<ValidationReport> ValidateAsync(byte[] xml, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var endpoint))
        {
            throw new ValidatorUnavailableException("Die offizielle Prüfung ist auf diesem Server nicht eingerichtet (Einstellung Validator:Url fehlt).");
        }

        if (!await Gate.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false))
        {
            throw new ValidatorUnavailableException("Der Prüfdienst ist gerade ausgelastet. Bitte versuche es gleich noch einmal.");
        }

        try
        {
            using var content = new ByteArrayContent(xml);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds)));

            using var response = await http.PostAsync(endpoint, content, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotAcceptable)
            {
                var body = await response.Content.ReadAsByteArrayAsync(timeout.Token).ConfigureAwait(false);
                return ValidationReportParser.Parse(body);
            }

            logger.LogWarning("Validator antwortete mit HTTP {StatusCode}", (int)response.StatusCode);
            throw new ValidatorUnavailableException(response.StatusCode == HttpStatusCode.UnprocessableEntity
                ? "Der Prüfdienst konnte die Datei nicht verarbeiten. Bitte melde das dem Betreiber dieses Servers."
                : $"Der Prüfdienst hat unerwartet geantwortet (HTTP {(int)response.StatusCode}).");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Validator unter {Endpoint} nicht erreichbar: {Error}", endpoint, ex.HttpRequestError);
            throw new ValidatorUnavailableException("Der Prüfdienst ist gerade nicht erreichbar. Bitte versuche es später noch einmal.", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Validator unter {Endpoint} hat nicht rechtzeitig geantwortet", endpoint);
            throw new ValidatorUnavailableException("Der Prüfdienst hat nicht rechtzeitig geantwortet. Bitte versuche es später noch einmal.", ex);
        }
        finally
        {
            Gate.Release();
        }
    }
}
