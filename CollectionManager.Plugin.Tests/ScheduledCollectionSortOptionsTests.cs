using CollectionManager.Plugin.Helpers;
using Xunit;

namespace CollectionManager.Plugin.Tests;

public sealed class ScheduledCollectionSortOptionsTests
{
    [Theory]
    [InlineData("DateCreatedDescending", true)]
    [InlineData("datecreateddescending", true)]
    [InlineData("", false)]
    [InlineData("Name", false)]
    public void IsDateCreatedDescending_DetectsSupportedSort(string sortBy, bool expected)
    {
        Assert.Equal(expected, ScheduledCollectionSortOptions.IsDateCreatedDescending(sortBy));
    }
}
