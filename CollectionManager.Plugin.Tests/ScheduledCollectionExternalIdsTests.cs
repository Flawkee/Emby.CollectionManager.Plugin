using CollectionManager.Plugin.Helpers;
using Xunit;

namespace CollectionManager.Plugin.Tests;

public sealed class ScheduledCollectionExternalIdsTests
{
    [Fact]
    public void ExtractImdbIdsFromText_HandlesUrlsCsvAndDuplicates()
    {
        var ids = ScheduledCollectionExternalIds.ExtractImdbIdsFromText("https://www.imdb.com/title/tt0111161/, tt0068646, TT0111161");

        Assert.Equal(new[] { "tt0111161", "tt0068646" }, ids);
    }

    [Theory]
    [InlineData("12345", "/lists/12345/items")]
    [InlineData("official:imdb-top-250", "/lists/official/imdb-top-250/items")]
    [InlineData("official:movies/moviemeter", "/lists/official/movies/moviemeter/items")]
    [InlineData("https://mdblist.com/lists/official/movies/moviemeter", "/lists/official/movies/moviemeter/items")]
    [InlineData("abdiel/halloween", "/lists/abdiel/halloween/items")]
    [InlineData("https://mdblist.com/lists/abdiel/halloween", "/lists/abdiel/halloween/items")]
    [InlineData("https://mdblist.com/lists/12345", "/lists/12345/items")]
    public void BuildMdblistItemsPath_NormalizesCommonInputs(string input, string expected)
    {
        Assert.Equal(expected, ScheduledCollectionExternalIds.BuildMdblistItemsPath(input));
    }

    [Fact]
    public void ExtractImdbIdsFromMdblistJson_ReadsTopLevelAndNestedIds()
    {
        const string json = "{\"movies\":[{\"imdb_id\":\"tt0111161\"},{\"ids\":{\"imdb\":\"tt0068646\"}}],\"shows\":[{\"imdb_id\":\"tt0903747\"}]}";

        Assert.Equal(new[] { "tt0111161", "tt0068646", "tt0903747" }, ScheduledCollectionExternalIds.ExtractImdbIdsFromMdblistJson(json));
    }

    [Fact]
    public void ExtractMdblistNextCursor_ReturnsEmptyForNullCursorInsteadOfNextQuotedField()
    {
        const string json = "{\"items\":[{\"imdb_id\":\"tt0111161\"}],\"next_cursor\":null,\"next\":\"not-a-cursor\"}";

        Assert.Equal(string.Empty, ScheduledCollectionExternalIds.ExtractMdblistNextCursor(json));
    }

    [Fact]
    public void ExtractMdblistNextCursor_ReturnsStringCursor()
    {
        const string json = "{\"items\":[],\"next_cursor\":\"abc123\"}";

        Assert.Equal("abc123", ScheduledCollectionExternalIds.ExtractMdblistNextCursor(json));
    }

    [Fact]
    public void BuildSimpleDefinition_UsesDirectImdbIdsWhenPasteContainsTitleIds()
    {
        var def = ScheduledCollectionExternalIds.BuildSimpleDefinition("", "https://www.imdb.com/title/tt0111161/ tt0068646");

        Assert.Equal("IMDb List Collection", def.Name);
        Assert.Equal("Both", def.ContentType);
        Assert.Equal(new[] { "tt0111161", "tt0068646" }, def.IncludedImdbIds);
        Assert.Equal(string.Empty, def.MdblistListPath);
        Assert.False(def.RemoveWhenInactive);
    }

    [Fact]
    public void BuildSimpleDefinition_UsesMdblistSourceWhenPasteLooksLikeList()
    {
        var def = ScheduledCollectionExternalIds.BuildSimpleDefinition("Watchlist", "https://mdblist.com/lists/abdiel/watchlist");

        Assert.Equal("Watchlist", def.Name);
        Assert.Empty(def.IncludedImdbIds);
        Assert.Equal("https://mdblist.com/lists/abdiel/watchlist", def.MdblistListPath);
    }
}
