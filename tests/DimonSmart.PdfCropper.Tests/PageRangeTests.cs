using Xunit;
using DimonSmart.PdfCropper;

namespace DimonSmart.PdfCropper.Tests;

public sealed class PageRangeTests
{
    [Fact]
    public void SinglePage_MatchesOnlyThatPage()
    {
        var range = new PageRange("3");

        Assert.False(range.HasError);
        Assert.True(range.Contains(3));
        Assert.False(range.Contains(2));
        Assert.False(range.Contains(4));
    }

    [Fact]
    public void ComplexRange_MatchesExpectedPages()
    {
        var range = new PageRange("1, 4-6, 8-");

        Assert.False(range.HasError);
        Assert.True(range.Contains(1));
        Assert.True(range.Contains(4));
        Assert.True(range.Contains(5));
        Assert.True(range.Contains(6));
        Assert.True(range.Contains(8));
        Assert.True(range.Contains(20));
        Assert.False(range.Contains(2));
        Assert.False(range.Contains(7));
    }

    [Fact]
    public void OpenStartRange_MatchesLeadingPages()
    {
        var range = new PageRange("-3");

        Assert.False(range.HasError);
        Assert.True(range.Contains(1));
        Assert.True(range.Contains(3));
        Assert.False(range.Contains(4));
    }

    [Fact]
    public void RangeWithSpaces_ParsesSuccessfully()
    {
        var range = new PageRange("1 - 3, 5");

        Assert.False(range.HasError);
        Assert.True(range.Contains(2));
        Assert.True(range.Contains(5));
        Assert.False(range.Contains(4));
    }

    [Theory]
    [InlineData(",")]
    [InlineData("1,,2")]
    [InlineData("-")]
    [InlineData("0")]
    [InlineData("2-0")]
    [InlineData("5-3")]
    [InlineData("1-2-3")]
    [InlineData("a")]
    [InlineData("1-a")]
    public void InvalidRange_SetsError(string expression)
    {
        var range = new PageRange(expression);

        Assert.True(range.HasError);
        Assert.False(string.IsNullOrWhiteSpace(range.ErrorMessage));
    }

    [Fact]
    public void Contains_ReturnsFalse_ForNonPositivePages()
    {
        var range = new PageRange("1-3");

        Assert.False(range.Contains(0));
        Assert.False(range.Contains(-1));
    }
}
