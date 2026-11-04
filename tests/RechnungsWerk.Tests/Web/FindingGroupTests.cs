using RechnungsWerk.Core.Validation;
using RechnungsWerk.Web.Infrastructure;

namespace RechnungsWerk.Tests.Web;

public sealed class FindingGroupTests
{
    [Theory]
    [InlineData(ValidationLevel.Error, 1, "1 Fehler")]
    [InlineData(ValidationLevel.Error, 3, "3 Fehler")]
    [InlineData(ValidationLevel.Warning, 1, "1 Warnung")]
    [InlineData(ValidationLevel.Warning, 2, "2 Warnungen")]
    [InlineData(ValidationLevel.Information, 1, "1 Hinweis")]
    [InlineData(ValidationLevel.Information, 4, "4 Hinweise")]
    public void Title_names_level_and_count(ValidationLevel level, int count, string expected)
    {
        Assert.Equal(expected, FindingGroup.TitleFor(level, count));
    }

    [Fact]
    public void Groups_list_errors_first_then_warnings_then_hints()
    {
        static ValidationMessage Message(ValidationLevel level, string rule) => new(level, rule, "", null, null, "");
        var report = new ValidationReport
        {
            IsAcceptable = false,
            Messages = [Message(ValidationLevel.Warning, "W1"), Message(ValidationLevel.Information, "I1"), Message(ValidationLevel.Error, "E1"),
                Message(ValidationLevel.Warning, "W2")],
        };

        var groups = FindingGroup.Of(report);

        Assert.Equal([ValidationLevel.Error, ValidationLevel.Warning, ValidationLevel.Information], groups.Select(g => g.Level));
        Assert.Equal(["W1", "W2"], groups[1].Messages.Select(m => m.RuleId));
        Assert.Equal("2 Warnungen", groups[1].Title);
    }
}
