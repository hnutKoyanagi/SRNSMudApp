// Components/Bounty/BountyBoardViewModelTests.cs
#region

using SRNSMudApp.Components.Bounty;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;

#endregion

namespace SRNSMudApp.Tests.Components.Bounty;

/// <summary>
///     <see cref="BountyBoardViewModel" /> の単体テスト。
/// </summary>
public class BountyBoardViewModelTests
{
    [Fact]
    public void ResolveRewardAsset_WhenBountyHasRewardAssetIdAndExistsInDictionary_ReturnsAsset()
    {
        var expectedAsset = new RightAsset { Id = 42, OwnerId = "test-owner", Amount = 100 };
        var bounty = new TaggingRequestEntity
        {
            OwnerId = "test-owner",
            Payload = new BountyPayload(42)
        };
        var rewardAssets = new Dictionary<int, RightAsset>
        {
            { 42, expectedAsset }
        };

        RightAsset? actual = BountyBoardViewModel.ResolveRewardAsset(bounty, rewardAssets);

        Assert.NotNull(actual);
        Assert.Equal(expectedAsset.Id, actual.Id);
        Assert.Equal(100, actual.Amount);
    }

    [Fact]
    public void ResolveRewardAsset_WhenBountyHasRewardAssetIdButNotInDictionary_ReturnsNull()
    {
        var bounty = new TaggingRequestEntity
        {
            OwnerId = "test-owner",
            Payload = new BountyPayload(99)
        };
        var rewardAssets = new Dictionary<int, RightAsset>();

        RightAsset? actual = BountyBoardViewModel.ResolveRewardAsset(bounty, rewardAssets);

        Assert.Null(actual);
    }

    [Fact]
    public void ResolveRewardAsset_WhenPayloadIsNotBountyPayload_ReturnsNull()
    {
        var bounty = new TaggingRequestEntity
        {
            OwnerId = "test-owner",
            Payload = new GratisPayload("message")
        };
        var rewardAssets = new Dictionary<int, RightAsset>();

        RightAsset? actual = BountyBoardViewModel.ResolveRewardAsset(bounty, rewardAssets);

        Assert.Null(actual);
    }

    [Fact]
    public void ResolveRewardAsset_WhenOfferedRewardAssetIdIsZero_ReturnsNull()
    {
        var bounty = new TaggingRequestEntity
        {
            OwnerId = "test-owner",
            Payload = new BountyPayload(0)
        };
        var rewardAssets = new Dictionary<int, RightAsset>
        {
            { 0, new RightAsset { Id = 0, OwnerId = "test-owner", Amount = 0 } }
        };

        RightAsset? actual = BountyBoardViewModel.ResolveRewardAsset(bounty, rewardAssets);

        Assert.Null(actual);
    }

    [Fact]
    public void ResolveRewardAsset_WhenArgumentsNull_ThrowsArgumentNullException()
    {
        var bounty = new TaggingRequestEntity { OwnerId = "test-owner" };
        var dict = new Dictionary<int, RightAsset>();

        _ = Assert.Throws<ArgumentNullException>(() => BountyBoardViewModel.ResolveRewardAsset(null!, dict));
        _ = Assert.Throws<ArgumentNullException>(() => BountyBoardViewModel.ResolveRewardAsset(bounty, null!));
    }

    [Theory]
    [InlineData("user-1", "user-2", true)]
    [InlineData("user-1", "user-1", false)]
    [InlineData("user-1", null, false)]
    [InlineData("user-1", "", false)]
    public void CanFulfillBounty_EvaluatesRequesterVsCurrentUser(
        string requesterUserId,
        string? currentUserId,
        bool expected)
    {
        var bounty = new TaggingRequestEntity
        {
            OwnerId = "test-owner",
            RequesterUserId = requesterUserId
        };

        var actual = BountyBoardViewModel.CanFulfillBounty(bounty, currentUserId);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanFulfillBounty_WhenBountyIsNull_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => BountyBoardViewModel.CanFulfillBounty(null!, "user-1"));
    }
}