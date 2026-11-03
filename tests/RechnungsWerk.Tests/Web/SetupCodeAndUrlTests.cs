using Microsoft.Extensions.Logging.Abstractions;
using RechnungsWerk.Web.Infrastructure;

namespace RechnungsWerk.Tests.Web;

public sealed class SetupCodeAndUrlTests
{
    [Fact]
    public void Setup_code_matches_regardless_of_dashes_case_and_confusable_letters()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rechnungswerk-setup-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SetupCodeStore(new DataPaths(folder), NullLogger<SetupCodeStore>.Instance);
            store.EnsureCreated();
            var written = File.ReadAllText(store.FilePath).Trim();

            Assert.Matches("^[0-9A-Z]{5}(-[0-9A-Z]{5}){3}$", written);
            Assert.True(store.Matches(written));
            Assert.True(store.Matches(written.Replace("-", " ", StringComparison.Ordinal).ToLowerInvariant()));
            Assert.True(store.Matches(written.Replace('0', 'O').Replace('1', 'I')));
            Assert.False(store.Matches(""));
            Assert.False(store.Matches("00000-00000-00000-00000"));

            // Ein zweiter Aufruf (Neustart) behält den Code.
            store.EnsureCreated();
            Assert.Equal(written, File.ReadAllText(store.FilePath).Trim());

            store.Delete();
            Assert.False(store.Matches(written));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData("/rechnungen/5", "/rechnungen/5")]
    [InlineData("/kunden?suche=a", "/kunden?suche=a")]
    [InlineData("https://boese.example/", "/rechnungen")]
    [InlineData("//boese.example/", "/rechnungen")]
    [InlineData("/\\boese.example", "/rechnungen")]
    [InlineData("javascript:alert(1)", "/rechnungen")]
    [InlineData("", "/rechnungen")]
    [InlineData(null, "/rechnungen")]
    public void Return_url_after_login_stays_inside_the_app(string? input, string expected) =>
        Assert.Equal(expected, LocalUrl.SafeReturnUrl(input));
}
