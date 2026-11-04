using RechnungsWerk.Core.Formatting;

namespace RechnungsWerk.Tests;

public sealed class GermanFormatTests
{
    [Theory]
    [InlineData("1.500", "1500")]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("1234,5", "1234.5")]
    [InlineData("1,500", "1.5")]
    [InlineData("0,335", "0.335")]
    [InlineData(" 95 ", "95")]
    [InlineData("12.345.678,9", "12345678.9")]
    [InlineData(",5", "0.5")]
    public void Reads_numbers_written_the_german_way(string text, string expected)
    {
        Assert.True(GermanFormat.TryParseDecimal(text, out var value));
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), value);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("1.50")]
    [InlineData("1.2345")]
    [InlineData("12.34.567")]
    [InlineData("1,2,3")]
    [InlineData("1e3")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_numbers_that_are_not_clearly_german(string? text)
    {
        Assert.False(GermanFormat.TryParseDecimal(text, out _));
    }
}
