using ArjuyScaffold.Support;
using Xunit;

namespace ArjuyScaffold.Tests;

public class PluralizerTests
{
    [Theory]
    [InlineData("Category", "Categories")]
    [InlineData("Edition", "Editions")]
    [InlineData("Address", "Addresses")]
    [InlineData("Box", "Boxes")]
    [InlineData("Church", "Churches")]
    [InlineData("Dish", "Dishes")]
    [InlineData("Day", "Days")]
    [InlineData("Person", "People")]
    [InlineData("OrderCategory", "OrderCategories")]
    [InlineData("Status", "Statuses")]
    [InlineData("Golf", "Golfs")]
    [InlineData("User", "Users")]
    public void Pluralize_ReturnsEnglishPlural(string singular, string expected)
    {
        Assert.Equal(expected, Pluralizer.Pluralize(singular));
    }
}
