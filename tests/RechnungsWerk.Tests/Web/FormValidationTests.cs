using System.ComponentModel.DataAnnotations;
using RechnungsWerk.Web.Services;

namespace RechnungsWerk.Tests.Web;

public sealed class FormValidationTests
{
    private static CustomerForm Customer(string country) => new()
    {
        Name = "Muster Ltd",
        Street = "1 High Street",
        PostalCode = "SW1A 1AA",
        City = "London",
        CountryCode = country,
        Email = "rechnung@example.org",
    };

    private static CompanyForm Company(string country) => new()
    {
        Name = "Muster Software GmbH",
        Street = "Musterstraße 1",
        PostalCode = "10115",
        City = "Berlin",
        CountryCode = country,
        TaxNumber = "12/345/67890",
        ContactName = "Erika Mustermann",
        Phone = "+49 30 1234567",
        Email = "rechnung@muster.example",
    };

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    private static bool HasCountryError(IEnumerable<ValidationResult> results) =>
        results.Any(r => r.MemberNames.Contains(nameof(CustomerForm.CountryCode)));

    [Theory]
    [InlineData("UK")]
    [InlineData("D1")]
    [InlineData("12")]
    [InlineData("DEU")]
    public void Country_codes_that_cannot_go_into_an_e_invoice_are_rejected(string country)
    {
        Assert.True(HasCountryError(Validate(Customer(country))), $"Kunde mit „{country}“");
        Assert.True(HasCountryError(Validate(Company(country))), $"Firma mit „{country}“");
    }

    [Theory]
    [InlineData("DE")]
    [InlineData("gb")]
    [InlineData("AT")]
    public void Known_country_codes_are_accepted(string country)
    {
        Assert.False(HasCountryError(Validate(Customer(country))));
        Assert.False(HasCountryError(Validate(Company(country))));
    }
}
