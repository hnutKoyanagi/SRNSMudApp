using Moq;

using MudBlazor;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using TagEntity = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Tests.Components.Tag;

public sealed class RequestTagPermissionViewModelTests
{
    private readonly Mock<IRightAssetDataProvider> _rightAssetDataProviderMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();
    private readonly RequestTagPermissionViewModel _sut;

    public RequestTagPermissionViewModelTests()
    {
        _sut = new RequestTagPermissionViewModel(
            _rightAssetDataProviderMock.Object,
            _snackbarMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_ConstructsSelectableUsers_ExcludingCurrentUser()
    {
        // Arrange
        const string currentUserId = "user-me";
        var tag = new TagEntity
        {
            Id = 1,
            Name = "Crypto",
            OwnerId = "user-alice",
            Owner = new ApplicationUser { Id = "user-alice", UserName = "Alice" }
        };

        var holders = new List<RightAssetHolderSummary>
        {
            new("user-bob", "Bob", 50, 1, 0, 0, DateTime.UtcNow),
            new("user-charlie", "Charlie", 0, 1, 0, 0, DateTime.UtcNow), // 残高0は除外
            new(currentUserId, "Me", 100, 1, 0, 0, DateTime.UtcNow) // 自分は除外
        };

        _rightAssetDataProviderMock
            .Setup(p => p.GetAvailableRightAssetsForUserAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new(10, 1, "Bitcoin", 5)]);

        // Act
        await _sut.InitializeAsync(tag, presetTargetUserId: null, holders, currentUserId);

        // Assert
        Assert.Equal(2, _sut.SelectableUsers.Count);
        Assert.Contains(_sut.SelectableUsers, u => u.UserId == "user-alice");
        Assert.Contains(_sut.SelectableUsers, u => u.UserId == "user-bob");
        Assert.DoesNotContain(_sut.SelectableUsers, u => u.UserId == "user-charlie");
        Assert.DoesNotContain(_sut.SelectableUsers, u => u.UserId == currentUserId);

        Assert.Equal("user-alice", _sut.SelectedTargetUserId);
        Assert.Single(_sut.MyAvailableAssets);
        Assert.Equal(5, _sut.MyAvailableAssets[0].Amount);
    }

    [Fact]
    public async Task InitializeAsync_WhenPresetTargetUserIdProvided_SelectsPresetUser()
    {
        // Arrange
        var tag = new TagEntity
        {
            Id = 1,
            Name = "AI",
            OwnerId = "user-alice",
            Owner = new ApplicationUser { Id = "user-alice", UserName = "Alice" }
        };

        var holders = new List<RightAssetHolderSummary>
        {
            new("user-bob", "Bob", 30, 1, 0, 0, DateTime.UtcNow)
        };

        // Act
        await _sut.InitializeAsync(tag, presetTargetUserId: "user-bob", holders, "user-me");

        // Assert
        Assert.Equal("user-bob", _sut.SelectedTargetUserId);
    }

    [Fact]
    public void GetSelectedAssetMaxAmount_ReturnsCorrectAmount()
    {
        // Assert before selection
        Assert.Equal(1, _sut.GetSelectedAssetMaxAmount());

        // Arrange with assets
        typeof(RequestTagPermissionViewModel)
            .GetProperty(nameof(RequestTagPermissionViewModel.MyAvailableAssets))!
            .SetValue(_sut, new List<UserAvailableRightAssetDto>
            {
                new(101, 1, "TagA", 25)
            });

        _sut.SelectedOfferedAssetId = 101;
        Assert.Equal(25, _sut.GetSelectedAssetMaxAmount());

        _sut.SelectedOfferedAssetId = 999;
        Assert.Equal(1, _sut.GetSelectedAssetMaxAmount());
    }

    [Fact]
    public async Task GetTargetUserAmountHelperText_ReturnsFormattedHelperText()
    {
        // Assert empty
        Assert.Equal(string.Empty, _sut.GetTargetUserAmountHelperText());

        // Arrange
        var tag = new TagEntity { Id = 1, Name = "Test", OwnerId = "user-alice" };
        var holders = new List<RightAssetHolderSummary> { new("user-alice", "Alice", 40, 1, 0, 0, DateTime.UtcNow) };
        await _sut.InitializeAsync(tag, "user-alice", holders, "user-me");

        // Assert
        Assert.Contains("相手の有効保有残高: 40", _sut.GetTargetUserAmountHelperText());
    }

    [Fact]
    public async Task SubmitAsync_WhenTargetUserEmpty_ShowsWarningAndReturnsFalse()
    {
        _sut.SelectedTargetUserId = null;

        bool result = await _sut.SubmitAsync(1, "user-me");

        Assert.False(result);
        _snackbarMock.Verify(s => s.Add("リクエスト先のユーザーを選択してください。", Severity.Warning, null, null), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_WhenCurrentUserEmpty_ShowsWarningAndReturnsFalse()
    {
        _sut.SelectedTargetUserId = "user-bob";

        bool result = await _sut.SubmitAsync(1, "");

        Assert.False(result);
        _snackbarMock.Verify(s => s.Add("ログインが必要です。", Severity.Warning, null, null), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_WhenRequestedAmountZeroOrNegative_ShowsWarningAndReturnsFalse()
    {
        _sut.SelectedTargetUserId = "user-bob";
        _sut.RequestedAmount = 0;

        bool result = await _sut.SubmitAsync(1, "user-me");

        Assert.False(result);
        _snackbarMock.Verify(s => s.Add("要求数量は1以上を入力してください。", Severity.Warning, null, null), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_WhenSuccessful_CallsDataProvider_AndReturnsTrue()
    {
        // Arrange
        _sut.SelectedTargetUserId = "user-bob";
        _sut.RequestedAmount = 5;
        _sut.SelectedOfferedAssetId = 20;
        _sut.OfferedAmount = 3;
        _sut.Message = "Please give me permissions";

        _rightAssetDataProviderMock
            .Setup(p => p.SubmitPermissionRequestAsync("user-me", It.IsAny<TagPermissionRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<bool>(true));

        // Act
        bool result = await _sut.SubmitAsync(42, "user-me");

        // Assert
        Assert.True(result);
        _rightAssetDataProviderMock.Verify(p => p.SubmitPermissionRequestAsync(
            "user-me",
            It.Is<TagPermissionRequestDto>(d =>
                d.RequestedTagId == 42 &&
                d.TargetUserId == "user-bob" &&
                d.RequestedAmount == 5 &&
                d.OfferedRightAssetId == 20 &&
                d.OfferedAmount == 3 &&
                d.Message == "Please give me permissions"),
            It.IsAny<CancellationToken>()), Times.Once);

        _snackbarMock.Verify(s => s.Add("操作権限のリクエストを送信しました。", Severity.Success, null, null), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_WhenFailed_ShowsErrorAndReturnsFalse()
    {
        // Arrange
        _sut.SelectedTargetUserId = "user-bob";
        _sut.RequestedAmount = 1;

        _rightAssetDataProviderMock
            .Setup(p => p.SubmitPermissionRequestAsync(It.IsAny<string>(), It.IsAny<TagPermissionRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure("既にリクエストが存在します。"));

        // Act
        bool result = await _sut.SubmitAsync(10, "user-me");

        // Assert
        Assert.False(result);
        _snackbarMock.Verify(s => s.Add("既にリクエストが存在します。", Severity.Error, null, null), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_WhenThrowsException_CatchesAndShowsError()
    {
        // Arrange
        _sut.SelectedTargetUserId = "user-bob";
        _sut.RequestedAmount = 1;

        _rightAssetDataProviderMock
            .Setup(p => p.SubmitPermissionRequestAsync(It.IsAny<string>(), It.IsAny<TagPermissionRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        // Act
        bool result = await _sut.SubmitAsync(10, "user-me");

        // Assert
        Assert.False(result);
        _snackbarMock.Verify(s => s.Add(It.Is<string>(msg => msg.Contains("DB error")), Severity.Error, null, null), Times.Once);
    }
}