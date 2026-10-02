// Components/Bounty/BountyBoardViewModelTests.cs
#region

using Moq;

using MudBlazor;

using SRNSMudApp.Components.Bounty;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Tests.Components.Bounty;

/// <summary>
///     <see cref="BountyBoardViewModel" /> の単体テスト。
/// </summary>
public class BountyBoardViewModelTests
{
    private readonly Mock<IBountyDataProvider> _bountyDataMock = new();

    private BountyBoardViewModel CreateViewModel() => new(_bountyDataMock.Object);

    [Fact]
    public void Constructor_WhenBountyDataIsNull_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new BountyBoardViewModel(null!));
    }

    [Fact]
    public async Task LoadDataAsync_PopulatesBountiesAndRewardAssetsAndSetsIsLoadingFalse()
    {
        // Arrange
        var bounty = new TaggingRequestEntity { Id = 1, OwnerId = "owner-1" };
        var asset = new RightAsset { Id = 10, OwnerId = "owner-1" };
        var boardData = new BountyBoardData([bounty], new Dictionary<int, RightAsset> { { 10, asset } });

        _bountyDataMock.Setup(x => x.GetActiveBountiesAsync()).ReturnsAsync(boardData);

        var vm = CreateViewModel();
        Assert.True(vm.IsLoading);

        // Act
        await vm.LoadDataAsync();

        // Assert
        Assert.False(vm.IsLoading);
        Assert.Single(vm.Bounties);
        Assert.Equal(1, vm.Bounties[0].Id);
        Assert.Single(vm.RewardAssets);
        Assert.Equal(10, vm.RewardAssets[10].Id);
        _bountyDataMock.Verify(x => x.GetActiveBountiesAsync(), Times.Once);
    }

    [Fact]
    public void CanFulfillBounty_InstanceMethod_UsesCurrentUserId()
    {
        var vm = CreateViewModel();
        vm.CurrentUserId = "user-1";

        var foreignBounty = new TaggingRequestEntity { OwnerId = "owner-1", RequesterUserId = "user-2" };
        var ownBounty = new TaggingRequestEntity { OwnerId = "owner-1", RequesterUserId = "user-1" };

        Assert.True(vm.CanFulfillBounty(foreignBounty));
        Assert.False(vm.CanFulfillBounty(ownBounty));
    }

    [Fact]
    public void ResolveRewardAsset_InstanceMethod_UsesRewardAssetsProperty()
    {
        var asset = new RightAsset { Id = 42, OwnerId = "owner-1" };
        var vm = CreateViewModel();
        vm.RewardAssets[42] = asset;

        var bountyWithReward = new TaggingRequestEntity { OwnerId = "owner-1", Payload = new BountyPayload(42) };
        var bountyWithoutReward = new TaggingRequestEntity { OwnerId = "owner-1", Payload = new BountyPayload(99) };

        var resolved = vm.ResolveRewardAsset(bountyWithReward);
        var notResolved = vm.ResolveRewardAsset(bountyWithoutReward);

        Assert.NotNull(resolved);
        Assert.Equal(42, resolved.Id);
        Assert.Null(notResolved);
    }

    [Fact]
    public void CreateDialogOptions_ReturnsExpectedOptions()
    {
        var options = BountyBoardViewModel.CreateDialogOptions();
        Assert.True(options.CloseOnEscapeKey);
        Assert.Equal(MaxWidth.Small, options.MaxWidth);
        Assert.True(options.FullWidth);
    }

    [Fact]
    public void FulfillDialogParameters_ReturnsValidParameters()
    {
        var bounty = new TaggingRequestEntity { Id = 5, OwnerId = "owner-1" };
        var parameters = BountyBoardViewModel.FulfillDialogParameters(bounty);

        Assert.NotNull(parameters);
        Assert.Same(bounty, parameters[nameof(FulfillBountyDialog.Bounty)]);
    }

    [Fact]
    public void FulfillDialogParameters_WhenBountyIsNull_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => BountyBoardViewModel.FulfillDialogParameters(null!));
    }

    [Fact]
    public void FulfillDialogOptions_ReturnsExpectedOptions()
    {
        var options = BountyBoardViewModel.FulfillDialogOptions();
        Assert.True(options.CloseOnEscapeKey);
        Assert.Equal(MaxWidth.Small, options.MaxWidth);
        Assert.True(options.FullWidth);
    }

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