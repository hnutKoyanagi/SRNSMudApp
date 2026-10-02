using System.Security.Claims;

using Moq;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using TagEntity = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Tests.Components.Tag;

/// <summary>
///     <see cref="TagDetailViewModel"/> の単体テスト。
///     DOM 描画を伴わずに高速かつ決定論的に状態変化および各アクションを検証する。
/// </summary>
public sealed class TagDetailViewModelTests
{
    private readonly Mock<ITagDetailDataProvider> _tagDetailDataMock = new();
    private readonly Mock<ITagCommandService> _tagCommandServiceMock = new();
    private readonly Mock<ITagContentProposalService> _contentProposalMock = new();
    private readonly Mock<ITagNameProposalService> _nameProposalMock = new();
    private readonly Mock<ITagLockService> _tagLockServiceMock = new();

    private readonly TagDetailViewModel _sut;

    public TagDetailViewModelTests()
    {
        _sut = new TagDetailViewModel(
            _tagDetailDataMock.Object,
            _tagCommandServiceMock.Object,
            _contentProposalMock.Object,
            _nameProposalMock.Object,
            _tagLockServiceMock.Object);
    }

    private static TagDetailPageData CreateSamplePageData(TagEntity tag)
    {
        return new TagDetailPageData(
            Tag: tag,
            IsFollowing: false,
            RelatedItems: [],
            RelatedTags: [],
            WeightLedgers: [],
            PublicOffers: [],
            PendingRequests: [],
            RightAssetOverview: null);
    }

    [Fact]
    public void SetUserContext_WithClaims_SetsUserIdAndAdmin()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // Act
        _sut.SetUserContext(principal);

        // Assert
        Assert.Equal("user-123", _sut.CurrentUserId);
        Assert.True(_sut.IsAdmin);
    }

    [Fact]
    public void SetUserContext_NullPrincipal_ClearsState()
    {
        // Arrange
        _sut.SetUserContext("some-user", true);

        // Act
        _sut.SetUserContext(null as ClaimsPrincipal);

        // Assert
        Assert.Null(_sut.CurrentUserId);
        Assert.False(_sut.IsAdmin);
    }

    [Fact]
    public void SetUserContext_DirectParams_SetsState()
    {
        // Act
        _sut.SetUserContext("direct-user", false);

        // Assert
        Assert.Equal("direct-user", _sut.CurrentUserId);
        Assert.False(_sut.IsAdmin);
    }

    [Fact]
    public async Task LoadDataAsync_PopulatesAllProperties()
    {
        // Arrange
        const int tagId = 10;
        var tag = new TagEntity { Id = tagId, Name = "TestTag", OwnerId = "owner-1" };
        var pageData = CreateSamplePageData(tag);

        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(tagId, It.IsAny<string?>()))
            .ReturnsAsync(pageData);
        _contentProposalMock.Setup(c => c.GetPendingProposalsForTagAsync(tagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _nameProposalMock.Setup(n => n.GetPendingProposalsForTagAsync(tagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _tagLockServiceMock.Setup(l => l.AreAncestorsLockedAsync(tagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tagLockServiceMock.Setup(l => l.IsTagOrSiblingLockedAsync(tagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _sut.LoadDataAsync(tagId);

        // Assert
        Assert.Equal(tag, _sut.Tag);
        Assert.True(_sut.AreAncestorsLocked);
        Assert.False(_sut.IsTagOrSiblingLocked);
        Assert.Empty(_sut.PendingProposals);
        Assert.Empty(_sut.PendingNameProposals);
        Assert.Equal(0, _sut.TotalPendingProposalsCount);
    }

    [Fact]
    public async Task ToggleThisTagLockAsync_WhenNotAdmin_ReturnsFailure()
    {
        // Arrange
        _sut.SetUserContext("user-1", false);

        // Act
        var result = await _sut.ToggleThisTagLockAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("権限がない", result.Message);
    }

    [Fact]
    public async Task ToggleThisTagLockAsync_WhenAdmin_TogglesLockAndReloads()
    {
        // Arrange
        const int tagId = 10;
        var tag = new TagEntity { Id = tagId, Name = "TestTag", OwnerId = "owner-1", IsLocked = false };
        _sut.SetUserContext("admin-1", true);

        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(tagId, "admin-1"))
            .ReturnsAsync(CreateSamplePageData(tag));
        _tagLockServiceMock.Setup(l => l.ToggleTagLockAsync(tagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.LoadDataAsync(tagId);

        // Act
        var result = await _sut.ToggleThisTagLockAsync();

        // Assert
        Assert.True(result.Success);
        Assert.True(result.IsLocked);
        Assert.Contains("個別ロックしました", result.Message);
        _tagLockServiceMock.Verify(l => l.ToggleTagLockAsync(tagId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleThisTagLockAsync_WhenExceptionThrown_ReturnsFailure()
    {
        // Arrange
        const int tagId = 10;
        var tag = new TagEntity { Id = tagId, Name = "TestTag", OwnerId = "owner-1", IsLocked = false };
        _sut.SetUserContext("admin-1", true);

        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(tagId, "admin-1"))
            .ReturnsAsync(CreateSamplePageData(tag));
        _tagLockServiceMock.Setup(l => l.ToggleTagLockAsync(tagId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        await _sut.LoadDataAsync(tagId);

        // Act
        var result = await _sut.ToggleThisTagLockAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("DB error", result.Message);
    }

    [Fact]
    public async Task ToggleAncestorsLockAsync_WhenNotAdmin_ReturnsFailure()
    {
        // Arrange
        _sut.SetUserContext("user-1", false);

        // Act
        var result = await _sut.ToggleAncestorsLockAsync(true);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("権限がない", result.Message);
    }

    [Fact]
    public async Task ToggleAncestorsLockAsync_WhenLockTrue_CallsLockAncestorsAndReloads()
    {
        // Arrange
        const int tagId = 10;
        var tag = new TagEntity { Id = tagId, Name = "TestTag", OwnerId = "owner-1" };
        _sut.SetUserContext("admin-1", true);

        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(tagId, "admin-1"))
            .ReturnsAsync(CreateSamplePageData(tag));

        await _sut.LoadDataAsync(tagId);

        // Act
        var result = await _sut.ToggleAncestorsLockAsync(true);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("祖先をロックしました", result.Message);
        _tagLockServiceMock.Verify(l => l.LockAncestorsAsync(tagId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleAncestorsLockAsync_WhenLockFalse_CallsUnlockAncestorsAndReloads()
    {
        // Arrange
        const int tagId = 10;
        var tag = new TagEntity { Id = tagId, Name = "TestTag", OwnerId = "owner-1" };
        _sut.SetUserContext("admin-1", true);

        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(tagId, "admin-1"))
            .ReturnsAsync(CreateSamplePageData(tag));

        await _sut.LoadDataAsync(tagId);

        // Act
        var result = await _sut.ToggleAncestorsLockAsync(false);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("祖先のロックを解除しました", result.Message);
        _tagLockServiceMock.Verify(l => l.UnlockAncestorsAsync(tagId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleFollowAsync_WhenUserNull_ReturnsFalse()
    {
        // Act
        bool result = await _sut.ToggleFollowAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ToggleFollowAsync_WhenUserNotNull_TogglesAndReturnsNewState()
    {
        // Arrange
        const int tagId = 10;
        var tag = new TagEntity { Id = tagId, Name = "TestTag", OwnerId = "owner-1" };
        _sut.SetUserContext("user-1", false);

        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(tagId, "user-1"))
            .ReturnsAsync(CreateSamplePageData(tag));
        _tagDetailDataMock.Setup(d => d.ToggleFollowAsync(tagId, "user-1"))
            .ReturnsAsync(true);

        await _sut.LoadDataAsync(tagId);

        // Act
        bool result = await _sut.ToggleFollowAsync();

        // Assert
        Assert.True(result);
        Assert.True(_sut.IsFollowing);
    }

    [Fact]
    public async Task UpdateAutoAcceptAsync_WhenTagNull_ReturnsFalse()
    {
        // Act
        bool result = await _sut.UpdateAutoAcceptAsync(true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateAutoAcceptAsync_WhenTagPresent_CallsServiceAndUpdatesTag()
    {
        // Arrange
        const int tagId = 10;
        var tag = new TagEntity
        {
            Id = tagId,
            Name = "TestTag",
            Content = "Description",
            OwnerId = "owner-1",
            AutoAcceptIncomingTaggingRequests = false
        };
        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(tagId, It.IsAny<string?>()))
            .ReturnsAsync(CreateSamplePageData(tag));

        _tagCommandServiceMock.Setup(c => c.UpdateTagAsync(tagId, "TestTag", "Description", true, It.IsAny<List<int>>()))
            .ReturnsAsync(true);

        await _sut.LoadDataAsync(tagId);

        // Act
        bool result = await _sut.UpdateAutoAcceptAsync(true);

        // Assert
        Assert.True(result);
        Assert.True(_sut.Tag!.AutoAcceptIncomingTaggingRequests);
    }

    [Fact]
    public async Task DeleteTagAsync_WhenTagNull_ReturnsNotFound()
    {
        // Act
        var result = await _sut.DeleteTagAsync();

        // Assert
        Assert.Equal(TagDeleteOperationResult.NotFound, result);
    }

    [Fact]
    public async Task DeleteTagAsync_WhenLockedAndNotAdmin_ReturnsLocked()
    {
        // Arrange
        const int tagId = 10;
        var tag = new TagEntity { Id = tagId, Name = "TestTag", OwnerId = "user-1" };
        _sut.SetUserContext("user-1", false);

        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(tagId, "user-1"))
            .ReturnsAsync(CreateSamplePageData(tag));
        _tagLockServiceMock.Setup(l => l.IsTagOrSiblingLockedAsync(tagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.LoadDataAsync(tagId);

        // Act
        var result = await _sut.DeleteTagAsync();

        // Assert
        Assert.Equal(TagDeleteOperationResult.Locked, result);
    }

    [Fact]
    public async Task DeleteTagAsync_WhenSystemTag_ReturnsSystemTag()
    {
        // Arrange
        const int tagId = 10;
        var tag = new TagEntity { Id = tagId, Name = "TestTag", OwnerId = "admin-1", IsSystem = true };
        _sut.SetUserContext("admin-1", true);

        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(tagId, "admin-1"))
            .ReturnsAsync(CreateSamplePageData(tag));

        await _sut.LoadDataAsync(tagId);

        // Act
        var result = await _sut.DeleteTagAsync();

        // Assert
        Assert.Equal(TagDeleteOperationResult.SystemTag, result);
    }

    [Fact]
    public async Task DeleteTagAsync_WhenValidOwner_CallsDataProvider()
    {
        // Arrange
        const int tagId = 10;
        var tag = new TagEntity { Id = tagId, Name = "TestTag", OwnerId = "user-1", IsSystem = false };
        _sut.SetUserContext("user-1", false);

        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(tagId, "user-1"))
            .ReturnsAsync(CreateSamplePageData(tag));
        _tagDetailDataMock.Setup(d => d.DeleteTagWithResultAsync(tagId, "user-1", false))
            .ReturnsAsync(TagDeleteOperationResult.Success);

        await _sut.LoadDataAsync(tagId);

        // Act
        var result = await _sut.DeleteTagAsync();

        // Assert
        Assert.Equal(TagDeleteOperationResult.Success, result);
        _tagDetailDataMock.Verify(d => d.DeleteTagWithResultAsync(tagId, "user-1", false), Times.Once);
    }

    [Fact]
    public async Task ProposalOperations_WhenNotLoggedIn_ReturnsFailure()
    {
        // Arrange
        _sut.SetUserContext(null, false);

        // Act & Assert
        var approveResult = await _sut.ApproveContentProposalAsync(1);
        Assert.True(approveResult is Failure);

        var rejectResult = await _sut.RejectContentProposalAsync(1, "reason");
        Assert.True(rejectResult is Failure);

        var cancelResult = await _sut.CancelContentProposalAsync(1);
        Assert.True(cancelResult is Failure);

        var approveNameResult = await _sut.ApproveNameProposalAsync(1);
        Assert.True(approveNameResult is Failure);

        var rejectNameResult = await _sut.RejectNameProposalAsync(1, "reason");
        Assert.True(rejectNameResult is Failure);

        var cancelNameResult = await _sut.CancelNameProposalAsync(1);
        Assert.True(cancelNameResult is Failure);
    }

    [Fact]
    public async Task ApproveContentProposalAsync_WhenSuccess_CallsServiceAndReloads()
    {
        // Arrange
        const int tagId = 10;
        var tag = new TagEntity { Id = tagId, Name = "TestTag", OwnerId = "owner-1" };
        _sut.SetUserContext("owner-1", false);

        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(tagId, "owner-1"))
            .ReturnsAsync(CreateSamplePageData(tag));
        _contentProposalMock.Setup(c => c.ApproveProposalAsync(1, "owner-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<TagEntity>(tag));

        await _sut.LoadDataAsync(tagId);

        // Act
        var result = await _sut.ApproveContentProposalAsync(1);

        // Assert
        Assert.True(result is Success<TagEntity>);
        _contentProposalMock.Verify(c => c.ApproveProposalAsync(1, "owner-1", It.IsAny<CancellationToken>()), Times.Once);
        _tagDetailDataMock.Verify(d => d.GetTagDetailAsync(tagId, "owner-1"), Times.Exactly(2));
    }

    [Fact]
    public void ComputedProperties_CheckVariousConditions()
    {
        // Arrange
        var normalTag = new TagEntity { Id = 10, Name = "NormalTag", OwnerId = "user-1", IsSystem = false };
        var rootTag = new TagEntity { Id = 1, Name = TagEntity.RootTagName, OwnerId = "admin-1", IsSystem = false };

        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(10, "user-1"))
            .ReturnsAsync(CreateSamplePageData(normalTag));
        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(1, "admin-1"))
            .ReturnsAsync(CreateSamplePageData(rootTag));

        // Test non-admin owner
        _sut.SetUserContext("user-1", false);
        _ = _sut.LoadDataAsync(10);
        Assert.True(_sut.CanEdit);
        Assert.True(_sut.CanDelete);
        Assert.True(_sut.IsOwnerOrAdmin);
        Assert.False(_sut.CanShowLockSwitches);

        // Test root tag with admin
        _sut.SetUserContext("admin-1", true);
        _ = _sut.LoadDataAsync(1);
        Assert.False(_sut.CanShowLockSwitches);
    }
}