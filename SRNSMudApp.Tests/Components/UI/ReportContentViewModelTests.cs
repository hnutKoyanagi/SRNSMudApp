#region

using Moq;

using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Tests.Components.UI;

public class ReportContentViewModelTests
{
    private readonly Mock<IContentReportService> _contentReportServiceMock = new();
    private readonly ReportContentViewModel _sut;

    public ReportContentViewModelTests()
    {
        _sut = new ReportContentViewModel(_contentReportServiceMock.Object);
    }

    [Fact]
    public void Constructor_NullService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ReportContentViewModel(null!));
    }

    [Fact]
    public void InitialState_IsConfiguredProperly()
    {
        Assert.Equal(ReportTargetType.Item, _sut.TargetType);
        Assert.Null(_sut.ItemId);
        Assert.Null(_sut.TagId);
        Assert.Empty(_sut.TargetContent);
        Assert.Null(_sut.TargetOwnerName);
        Assert.Empty(_sut.CurrentUserId);
        Assert.Equal("スパム・宣伝目的", _sut.SelectedReason);
        Assert.Empty(_sut.Detail);
        Assert.False(_sut.IsSubmitting);
        Assert.False(_sut.CanSubmit);
    }

    [Fact]
    public void Initialize_ConfiguresPropertiesAndResetsForm()
    {
        // Act
        _sut.Initialize(ReportTargetType.Tag, null, 42, "Some harmful text", "Alice", "user-100");

        // Assert
        Assert.Equal(ReportTargetType.Tag, _sut.TargetType);
        Assert.Null(_sut.ItemId);
        Assert.Equal(42, _sut.TagId);
        Assert.Equal("Some harmful text", _sut.TargetContent);
        Assert.Equal("Alice", _sut.TargetOwnerName);
        Assert.Equal("user-100", _sut.CurrentUserId);
        Assert.Equal("スパム・宣伝目的", _sut.SelectedReason);
        Assert.Empty(_sut.Detail);
        Assert.False(_sut.IsSubmitting);
        Assert.True(_sut.CanSubmit);
    }

    [Theory]
    [InlineData("スパム・宣伝目的", "user-1", true)]
    [InlineData("", "user-1", false)]
    [InlineData("   ", "user-1", false)]
    [InlineData("スパム・宣伝目的", "", false)]
    [InlineData("スパム・宣伝目的", "   ", false)]
    public void CanSubmit_ValidatesConditions(string reason, string userId, bool expected)
    {
        // Arrange
        _sut.SelectedReason = reason;
        _sut.CurrentUserId = userId;

        // Assert
        Assert.Equal(expected, _sut.CanSubmit);
    }

    [Fact]
    public async Task SubmitAsync_WhenUserIdEmpty_ThrowsInvalidOperationException()
    {
        _sut.CurrentUserId = "";
        _sut.SelectedReason = "スパム";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SubmitAsync());
        Assert.Contains("通報するにはログインが必要です", ex.Message);
    }

    [Fact]
    public async Task SubmitAsync_WhenReasonEmpty_ThrowsInvalidOperationException()
    {
        _sut.CurrentUserId = "user-1";
        _sut.SelectedReason = "   ";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SubmitAsync());
        Assert.Contains("通報の理由を選択してください", ex.Message);
    }

    [Fact]
    public async Task SubmitAsync_WhenValid_CallsServiceAndReturnsReport()
    {
        // Arrange
        _sut.Initialize(ReportTargetType.Item, 12, null, "Harassment text", "Bob", "reporter-1");
        _sut.SelectedReason = "  誹謗中傷・嫌がらせ  ";
        _sut.Detail = "  Specific insulting lines  ";

        var expectedReport = new ContentReport
        {
            Id = 99,
            TargetType = ReportTargetType.Item,
            ItemId = 12,
            Reason = "誹謗中傷・嫌がらせ",
            Detail = "Specific insulting lines",
            OwnerId = "reporter-1"
        };

        _contentReportServiceMock.Setup(s => s.CreateReportAsync(
                It.Is<CreateContentReportDto>(dto =>
                    dto.TargetType == ReportTargetType.Item &&
                    dto.ItemId == 12 &&
                    dto.TagId == null &&
                    dto.Reason == "誹謗中傷・嫌がらせ" &&
                    dto.Detail == "Specific insulting lines"),
                "reporter-1"))
            .ReturnsAsync(expectedReport);

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        Assert.Same(expectedReport, result);
        Assert.False(_sut.IsSubmitting);
        _contentReportServiceMock.Verify(s => s.CreateReportAsync(It.IsAny<CreateContentReportDto>(), "reporter-1"), Times.Once);
    }
}