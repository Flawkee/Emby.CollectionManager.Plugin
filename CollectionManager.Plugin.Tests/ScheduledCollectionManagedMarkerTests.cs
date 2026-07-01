using CollectionManager.Plugin.Configuration;
using CollectionManager.Plugin.Helpers;
using Xunit;

namespace CollectionManager.Plugin.Tests;

public sealed class ScheduledCollectionManagedMarkerTests
{
    [Fact]
    public void BuildOverview_IncludesStableMarkerAndDefinitionName()
    {
        var overview = ScheduledCollectionManagedMarker.BuildOverview(new ScheduledCollectionDefinition
        {
            Name = "Halloween Movies",
            ContentType = "Movies",
            MatchMode = "All"
        });

        Assert.Contains("Managed by Collection Manager", overview);
        Assert.Contains("Definition: Halloween Movies", overview);
        Assert.Contains("Content: Movies", overview);
    }

    [Fact]
    public void IsManaged_ReturnsTrueOnlyForMarkerText()
    {
        Assert.True(ScheduledCollectionManagedMarker.IsManaged("Managed by Collection Manager\nDefinition: Test"));
        Assert.False(ScheduledCollectionManagedMarker.IsManaged("My manually created Halloween collection"));
        Assert.False(ScheduledCollectionManagedMarker.IsManaged(default!));
    }
}
