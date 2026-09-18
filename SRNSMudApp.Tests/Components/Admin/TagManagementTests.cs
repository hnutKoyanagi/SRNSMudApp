using Bunit;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Admin;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Admin;

/// <summary>
///     管理者用タグ管理画面 (<see cref="TagManagement" />) の bUnit コンポーネントテスト。
/// </summary>
public sealed class TagManagementTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly Mock<ITagLockService> _tagLockServiceMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();

    public TagManagementTests()
    {
        _ = _ctx.Services.AddAuth("admin-user-id", "Admin");
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _tagLockServiceMock.Object);
        _ = _ctx.Services.AddScoped(_ => _snackbarMock.Object);

        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public void TagManagement_RendersHierarchyLockSetting_AndTagList()
    {
        // Arrange
        _ = _tagLockServiceMock.Setup(s => s.GetLockedHierarchyLevelAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var testTags = new List<TagLockItemDto>
        {
            new(1, "RootTag", 1, null, null, "Alice", false, true, false, true),
            new(2, "ChildTag", 2, 1, "RootTag", "Bob", false, true, false, true),
            new(3, "LeafTag", 3, 2, "ChildTag", "Charlie", false, false, false, false)
        };
        _ = _tagLockServiceMock.Setup(s => s.GetAllTagsWithLockStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(testTags);

        var httpContext = new DefaultHttpContext();

        // Act
        IRenderedComponent<TagManagement> component =
            _ctx.Render<TagManagement>(parameters => parameters.AddCascadingValue(httpContext));

        // Assert
        Assert.False(string.IsNullOrEmpty(component.Markup), "Markup was: " + component.Markup);
        Assert.Contains("階層ロック設定", component.Markup);
        Assert.Contains("タグ管理", component.Markup);
        Assert.Contains("階層ロック設定", component.Markup);
        Assert.Contains("n階層目までのタグをロックします", component.Markup);
        Assert.Contains("RootTag", component.Markup);
        Assert.Contains("ChildTag", component.Markup);
        Assert.Contains("LeafTag", component.Markup);
        Assert.Contains("階層ロック", component.Markup);
        Assert.Contains("未ロック", component.Markup);
    }

    [Fact]
    public async Task TagManagement_SaveHierarchyLevel_CallsService()
    {
        // Arrange
        _ = _tagLockServiceMock.Setup(s => s.GetLockedHierarchyLevelAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _ = _tagLockServiceMock.Setup(s => s.GetAllTagsWithLockStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var httpContext = new DefaultHttpContext();

        // Act
        IRenderedComponent<TagManagement> component =
            _ctx.Render<TagManagement>(parameters => parameters.AddCascadingValue(httpContext));

        component.WaitForState(() => component.Markup.Contains("保存"));

        // Find save button and click
        var saveButton = component.Find("button.mud-button-filled");
        saveButton.Click();

        // Assert
        _tagLockServiceMock.Verify(s => s.SetLockedHierarchyLevelAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TagManagement_ToggleTagLock_CallsService()
    {
        // Arrange
        _ = _tagLockServiceMock.Setup(s => s.GetLockedHierarchyLevelAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var testTags = new List<TagLockItemDto>
        {
            new(5, "CustomTag", 1, null, null, "Alice", false, false, false, false)
        };
        _ = _tagLockServiceMock.Setup(s => s.GetAllTagsWithLockStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(testTags);

        var httpContext = new DefaultHttpContext();

        // Act
        IRenderedComponent<TagManagement> component =
            _ctx.Render<TagManagement>(parameters => parameters.AddCascadingValue(httpContext));

        // Find "個別ロック" button
        var toggleButton = component.Find("button.mud-button-outlined");
        toggleButton.Click();

        // Assert
        _tagLockServiceMock.Verify(s => s.ToggleTagLockAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}