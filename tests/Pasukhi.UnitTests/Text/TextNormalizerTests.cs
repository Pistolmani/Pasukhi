using Pasukhi.Application.Text;

namespace Pasukhi.UnitTests.Text;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("  Hello,   PRICE!!!  ", "hello price")]
    [InlineData("ფასი რა არის?", "ფასი რა არის")]
    [InlineData("Price ფასი\tCost", "price ფასი cost")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_collapses_case_punctuation_and_whitespace(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }

    [Fact]
    public void Tokenize_preserves_georgian_tokens()
    {
        var tokens = TextNormalizer.Tokenize("რა ღირს ფასი?");

        Assert.Contains("რა", tokens);
        Assert.Contains("ღირს", tokens);
        Assert.Contains("ფასი", tokens);
    }

    [Fact]
    public void SplitCsv_supports_multiple_separators()
    {
        var values = TextNormalizer.SplitCsv("price, cost;ფასი|delivery\nshipping");

        Assert.Equal(new[] { "price", "cost", "ფასი", "delivery", "shipping" }, values);
    }
}
