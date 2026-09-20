using SRNSMudApp.Components.Item;
using SRNSMudApp.Data;

namespace SRNSMudApp.Tests.Components.Item;

public class ItemDetailRequestFilterTests
{
    private const string CurrentUserId = "user-me";
    private const string OtherUserId = "user-other";

    private static TaggingRequestEntity CreateRequest(
        int id,
        string requesterUserId,
        string tagName,
        string requesterUserName = "user",
        string tagOwnerUserName = "tag_owner")
    {
        var tag = new SRNSMudApp.Data.Tag
        {
            Id = id * 10,
            Name = tagName,
            OwnerId = requesterUserId,
            Owner = new ApplicationUser { UserName = tagOwnerUserName }
        };

        return new TaggingRequestEntity
        {
            Id = id,
            RequesterUserId = requesterUserId,
            OwnerId = requesterUserId,
            Owner = new ApplicationUser { Id = requesterUserId, UserName = requesterUserName },
            RequestedTagId = tag.Id,
            RequestedTag = tag
        };
    }

    [Fact]
    public void FilterRequests_WhenOnlyMyRequestsTrue_ReturnsOnlyCurrentUserRequests()
    {
        var req1 = CreateRequest(1, CurrentUserId, "真実");
        var req2 = CreateRequest(2, OtherUserId, "真実");
        var requests = new[] { req1, req2 };

        var result = ItemDetailRequestFilter.FilterRequests(
            requests,
            currentUserId: CurrentUserId,
            onlyMyRequests: true,
            searchQuery: null).ToList();

        Assert.Single(result);
        Assert.Equal(req1.Id, result[0].Id);
    }

    [Fact]
    public void FilterRequests_WhenOnlyMyRequestsFalse_ReturnsAllRequests()
    {
        var req1 = CreateRequest(1, CurrentUserId, "真実");
        var req2 = CreateRequest(2, OtherUserId, "真実");
        var requests = new[] { req1, req2 };

        var result = ItemDetailRequestFilter.FilterRequests(
            requests,
            currentUserId: CurrentUserId,
            onlyMyRequests: false,
            searchQuery: null).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FilterRequests_WhenSearchQueryMatchesTag_FiltersByTagName()
    {
        var req1 = CreateRequest(1, CurrentUserId, "真実");
        var req2 = CreateRequest(2, CurrentUserId, "善");
        var requests = new[] { req1, req2 };

        var result = ItemDetailRequestFilter.FilterRequests(
            requests,
            currentUserId: CurrentUserId,
            onlyMyRequests: true,
            searchQuery: "真実").ToList();

        Assert.Single(result);
        Assert.Equal(req1.Id, result[0].Id);
    }

    [Fact]
    public void FilterRequests_CombinesOnlyMyRequestsAndTagFilter()
    {
        var myShinji = CreateRequest(1, CurrentUserId, "真実");
        var myZen = CreateRequest(2, CurrentUserId, "善");
        var otherShinji = CreateRequest(3, OtherUserId, "真実");
        var otherZen = CreateRequest(4, OtherUserId, "善");
        var requests = new[] { myShinji, myZen, otherShinji, otherZen };

        // デフォルト: 自分のリクエストのみ ＆ 真実タグで絞り込み
        var defaultResult = ItemDetailRequestFilter.FilterRequests(
            requests,
            currentUserId: CurrentUserId,
            onlyMyRequests: true,
            searchQuery: "真実").ToList();

        Assert.Single(defaultResult);
        Assert.Equal(myShinji.Id, defaultResult[0].Id);

        // 「自分のリクエストのみ」チェックを外した場合: 全員の真実タグリクエスト
        var allUsersResult = ItemDetailRequestFilter.FilterRequests(
            requests,
            currentUserId: CurrentUserId,
            onlyMyRequests: false,
            searchQuery: "真実").ToList();

        Assert.Equal(2, allUsersResult.Count);
        Assert.Contains(allUsersResult, r => r.Id == myShinji.Id);
        Assert.Contains(allUsersResult, r => r.Id == otherShinji.Id);
    }

    [Fact]
    public void FilterRequests_TagWithUserSearch_FiltersByTagAndUser()
    {
        var req1 = CreateRequest(1, CurrentUserId, "真実", requesterUserName: "alice", tagOwnerUserName: "bob");
        var req2 = CreateRequest(2, CurrentUserId, "真実", requesterUserName: "charlie", tagOwnerUserName: "dave");
        var requests = new[] { req1, req2 };

        var result = ItemDetailRequestFilter.FilterRequests(
            requests,
            currentUserId: CurrentUserId,
            onlyMyRequests: false,
            searchQuery: "真実 @bob").ToList();

        Assert.Single(result);
        Assert.Equal(req1.Id, result[0].Id);
    }

    [Fact]
    public void FilterRequests_ResolvesTagNameFromAllTags_WhenRequestedTagIsNull()
    {
        var allTags = new[]
        {
            new SRNSMudApp.Data.Tag { Id = 100, Name = "ResolvedTag", OwnerId = CurrentUserId }
        };

        var req = new TaggingRequestEntity
        {
            Id = 1,
            OwnerId = CurrentUserId,
            RequesterUserId = CurrentUserId,
            RequestedTagId = 100,
            RequestedTag = null!
        };

        var result = ItemDetailRequestFilter.FilterRequests(
            [req],
            currentUserId: CurrentUserId,
            onlyMyRequests: false,
            searchQuery: "ResolvedTag",
            allTags: allTags).ToList();

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public void FilterRequests_WhenRequestsNull_ReturnsEmpty()
    {
        var result = ItemDetailRequestFilter.FilterRequests(
            null,
            currentUserId: CurrentUserId,
            onlyMyRequests: true,
            searchQuery: "真実");

        Assert.Empty(result);
    }
}