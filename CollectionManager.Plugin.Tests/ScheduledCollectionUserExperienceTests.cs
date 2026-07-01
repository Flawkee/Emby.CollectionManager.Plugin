using CollectionManager.Plugin.Configuration;
using CollectionManager.Plugin.Helpers;
using System;
using Xunit;

namespace CollectionManager.Plugin.Tests;

public sealed class ScheduledCollectionUserExperienceTests
{
    [Theory]
    [InlineData("official:movies/moviemeter", "IMDb MovieMeter Top Movies via MDBList")]
    [InlineData("https://mdblist.com/lists/official/movies/moviemeter", "IMDb MovieMeter Top Movies via MDBList")]
    [InlineData("official:movies/popular", "Popular Movies via MDBList")]
    [InlineData("official:shows/moviemeter", "IMDb MovieMeter Top TV Shows via MDBList")]
    public void FriendlySourceLabel_NamesBuiltInMdblistSources(string source, string expected)
    {
        var definition = new ScheduledCollectionDefinition { MdblistListPath = source };

        Assert.Equal(expected, ScheduledCollectionUserExperience.FriendlySourceLabel(definition));
    }

    [Fact]
    public void FriendlySourceLabel_SummarizesDirectImdbIds()
    {
        var definition = new ScheduledCollectionDefinition { IncludedImdbIds = new[] { "tt0111161", "tt0068646" } };

        Assert.Equal("2 direct IMDb title IDs", ScheduledCollectionUserExperience.FriendlySourceLabel(definition));
    }

    [Fact]
    public void BuildSetupChecklist_FlagsMissingMdblistApiKeyForFeaturedPresets()
    {
        var definitions = new[]
        {
            new ScheduledCollectionDefinition
            {
                Enabled = true,
                Name = "Top 100 Movies",
                ContentType = "Movies",
                MdblistListPath = "official:movies/moviemeter",
                MaxItems = 100
            }
        };

        var checklist = ScheduledCollectionUserExperience.BuildSetupChecklist(definitions, scheduledCollectionsEnabled: true, mdblistApiKeyConfigured: false);

        Assert.Contains(checklist, item => item.Key == "mdblist-api-key" && !item.IsOk && item.Detail.Contains("needed", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(checklist, item => item.Key == "ready-to-preview" && item.IsOk);
    }

    [Fact]
    public void BuildSetupChecklist_AllowsDirectImdbIdsWithoutMdblistApiKey()
    {
        var definitions = new[]
        {
            new ScheduledCollectionDefinition
            {
                Enabled = true,
                Name = "IMDb Picks",
                IncludedImdbIds = new[] { "tt0111161" }
            }
        };

        var checklist = ScheduledCollectionUserExperience.BuildSetupChecklist(definitions, scheduledCollectionsEnabled: true, mdblistApiKeyConfigured: false);

        Assert.Contains(checklist, item => item.Key == "mdblist-api-key" && item.IsOk && item.Detail.Contains("not needed", StringComparison.OrdinalIgnoreCase));
    }
}
