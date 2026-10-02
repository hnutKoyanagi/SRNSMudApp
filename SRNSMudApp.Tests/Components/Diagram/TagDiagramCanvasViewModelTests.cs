using Moq;

using SRNSMudApp.Components.Diagram;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Diagram;

/// <summary>
///     TagDiagramCanvasViewModel の単体テスト。
///     bUnit を使わずにエッジ生成の検証・サービス連携を検証する。
/// </summary>
public sealed class TagDiagramCanvasViewModelTests
{
    private const string CurrentUserId = "user-diagram";
    private readonly Mock<ITagEdgeService> _edgeServiceMock = new();
    private readonly TagDiagramCanvasViewModel _sut;

    public TagDiagramCanvasViewModelTests()
    {
        _sut = new TagDiagramCanvasViewModel(_edgeServiceMock.Object);
    }

    [Theory]
    [InlineData(1, 1, false, "同一タグ間にエッジを作成することはできません。")]
    [InlineData(0, 2, false, "無効なタグIDです。")]
    [InlineData(1, 2, true, null)]
    public void CanCreateEdge_ValidatesCorrectly(int sourceId, int targetId, bool expectedCanCreate, string? expectedError)
    {
        (bool canCreate, string? error) = TagDiagramCanvasViewModel.CanCreateEdge(sourceId, targetId);

        Assert.Equal(expectedCanCreate, canCreate);
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public async Task CreateEdgeAsync_WhenValid_CallsTagEdgeService()
    {
        var edge = new TagEdge { Id = 1, SourceTagId = 1, TargetTagId = 2, OwnerId = CurrentUserId };
        _edgeServiceMock
            .Setup(r => r.CreateEdgeAsync(1, 2, CurrentUserId))
            .ReturnsAsync(new Success<TagEdge>(edge));

        (bool success, string? error) = await _sut.CreateEdgeAsync(1, 2, CurrentUserId);

        Assert.True(success);
        Assert.Null(error);
        _edgeServiceMock.Verify(r => r.CreateEdgeAsync(1, 2, CurrentUserId), Times.Once);
    }
}