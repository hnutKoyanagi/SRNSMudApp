using Moq;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Tag;

/// <summary>
///     ItemTagChipActionViewModel の単体テスト。
///     bUnit を用いずに Weight 変更および契約提案ルーティングを検証する。
/// </summary>
public sealed class ItemTagChipActionViewModelTests
{
    private const string CurrentUserId = "user-123";
    private readonly Mock<IItemTagService> _itemTagServiceMock = new();
    private readonly ItemTagChipActionViewModel _sut;

    public ItemTagChipActionViewModelTests()
    {
        _sut = new ItemTagChipActionViewModel(_itemTagServiceMock.Object);
    }

    [Fact]
    public async Task UpdateWeightAsync_WhenOwner_CallsItemTagServiceDirectly()
    {
        // Arrange
        const int relationId = 42;
        const int delta = -1;
        var relation = new TagRelation
        {
            Id = relationId,
            OwnerId = CurrentUserId,
            Tag = new SRNSMudApp.Data.Tag { Id = 1, Name = "TestTag", OwnerId = CurrentUserId }
        };

        _itemTagServiceMock
            .Setup(s => s.UpdateTagWeightAsync(relationId, delta, CurrentUserId))
            .ReturnsAsync(UpdateWeightResult.Success);

        // Act
        TagWeightActionResult result = await _sut.UpdateWeightAsync(relation, delta, CurrentUserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TagWeightOperationType.ExecutedDirectly, result.OperationType);
        Assert.Equal(UpdateWeightResult.Success, result.UpdateResult);
        _itemTagServiceMock.Verify(s => s.UpdateTagWeightAsync(relationId, delta, CurrentUserId), Times.Once);
    }

    [Fact]
    public async Task UpdateWeightAsync_WhenNotOwnerAndTagExists_RoutesToProposedContract()
    {
        // Arrange
        var relation = new TagRelation
        {
            Id = 50,
            OwnerId = "other-user",
            Tag = new SRNSMudApp.Data.Tag { Id = 2, Name = "OtherTag", OwnerId = "other-user" }
        };

        // Act
        TagWeightActionResult result = await _sut.UpdateWeightAsync(relation, 1, CurrentUserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TagWeightOperationType.ProposedContract, result.OperationType);
        // オーナーではないため直接サービス更新は呼ばれない
        _itemTagServiceMock.Verify(s => s.UpdateTagWeightAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateWeightAsync_WhenServiceReturnsNotFound_ReturnsErrorResult()
    {
        // Arrange
        const int relationId = 99;
        var relation = new TagRelation
        {
            Id = relationId,
            OwnerId = CurrentUserId,
            Tag = new SRNSMudApp.Data.Tag { Id = 3, Name = "MissingTag", OwnerId = CurrentUserId }
        };

        _itemTagServiceMock
            .Setup(s => s.UpdateTagWeightAsync(relationId, 1, CurrentUserId))
            .ReturnsAsync(UpdateWeightResult.NotFound);

        // Act
        TagWeightActionResult result = await _sut.UpdateWeightAsync(relation, 1, CurrentUserId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("タグの関連付けが見つかりません。", result.ErrorMessage);
    }
}