using AngleSharp.Dom;

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Services;
using SRNSMudApp.Tests.TestSupport;

using TagEntity = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Tests.Components.Tag;

public sealed class TagDetailAncestorLockTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly Mock<ITagDetailDataProvider> _tagDetailDataMock = new();
    private readonly Mock<ITagLockService> _tagLockServiceMock = new();
    private readonly Mock<ITagContentProposalService> _contentProposalMock = new();
    private readonly Mock<ITagNameProposalService> _nameProposalMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();

    public TagDetailAncestorLockTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices();
        _ = _ctx.Services.AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _tagDetailDataMock.Object);
        _ = _ctx.Services.AddScoped(_ => _tagLockServiceMock.Object);
        _ = _ctx.Services.AddScoped(_ => _contentProposalMock.Object);
        _ = _ctx.Services.AddScoped(_ => _nameProposalMock.Object);
        _ = _ctx.Services.AddScoped(_ => _snackbarMock.Object);
        _ = _ctx.Services.AddAuthorizationCore();
        _ = _ctx.Services.AddAuth("admin-user", "Admin");
        _ = _ctx.Render<MudPopoverProvider>();

        _contentProposalMock.Setup(s => s.GetPendingProposalsForTagAsync(It.IsAny<int>()))
            .ReturnsAsync([]);
        _nameProposalMock.Setup(s => s.GetPendingProposalsForTagAsync(It.IsAny<int>()))
            .ReturnsAsync([]);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    private IRenderedComponent<TagDetail> RenderTagDetail(int tagId, string userId, params string[] roles)
    {
        return _ctx.Render<TagDetail>(parameters => parameters
            .Add(p => p.TagId, tagId)
            .AddCascadingValue(Task.FromResult(BunitTestSetup.CreateAuthState(userId, roles))));
    }

    [Fact]
    public void WhenNonAdminViewsTagDetail_LockSwitchesAreNotRendered()
    {
        var tag = new TagEntity
        {
            Id = 10,
            Name = "子タグ",
            OwnerId = "tag-owner",
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        var pageData = new TagDetailPageData(tag, false, [], [], [], [], []);
        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(10, "regular-user")).ReturnsAsync(pageData);
        _tagLockServiceMock.Setup(s => s.AreAncestorsLockedAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _tagLockServiceMock.Setup(s => s.IsTagOrSiblingLockedAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        IRenderedComponent<TagDetail> cut = RenderTagDetail(10, "regular-user");

        cut.WaitForState(() => cut.Markup.Contains("子タグ"));

        Assert.DoesNotContain("このTagをロック", cut.Markup);
        Assert.DoesNotContain("このTagの祖先をロック", cut.Markup);
    }

    [Fact]
    public void WhenAdminViewsTagDetail_AndNotLocked_RendersUnlockToggleSwitches()
    {
        var tag = new TagEntity
        {
            Id = 10,
            Name = "子タグ",
            OwnerId = "tag-owner",
            IsLocked = false,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        var pageData = new TagDetailPageData(tag, false, [], [], [], [], []);
        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(10, "admin-user")).ReturnsAsync(pageData);
        _tagLockServiceMock.Setup(s => s.AreAncestorsLockedAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _tagLockServiceMock.Setup(s => s.IsTagOrSiblingLockedAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        IRenderedComponent<TagDetail> cut = RenderTagDetail(10, "admin-user", "Admin");

        cut.WaitForState(() => cut.Markup.Contains("子タグ"));

        Assert.Contains("このTagをロック", cut.Markup);
        Assert.Contains("このTagの祖先をロック", cut.Markup);
    }

    [Fact]
    public void WhenAdminViewsTagDetail_AndLocked_RendersLockedToggleSwitches()
    {
        var tag = new TagEntity
        {
            Id = 10,
            Name = "子タグ",
            OwnerId = "tag-owner",
            IsLocked = true,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        var pageData = new TagDetailPageData(tag, false, [], [], [], [], []);
        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(10, "admin-user")).ReturnsAsync(pageData);
        _tagLockServiceMock.Setup(s => s.AreAncestorsLockedAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _tagLockServiceMock.Setup(s => s.IsTagOrSiblingLockedAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        IRenderedComponent<TagDetail> cut = RenderTagDetail(10, "admin-user", "Admin");

        cut.WaitForState(() => cut.Markup.Contains("子タグ"));

        Assert.Contains("このTagをロック中", cut.Markup);
        Assert.Contains("このTagの祖先をロック中", cut.Markup);
    }

    [Fact]
    public async Task WhenAdminTogglesThisTagLock_ToggleTagLockIsCalled()
    {
        var tag = new TagEntity
        {
            Id = 10,
            Name = "子タグ",
            OwnerId = "tag-owner",
            IsLocked = false,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        var pageData = new TagDetailPageData(tag, false, [], [], [], [], []);
        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(10, "admin-user")).ReturnsAsync(pageData);
        _tagLockServiceMock.Setup(s => s.AreAncestorsLockedAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _tagLockServiceMock.Setup(s => s.IsTagOrSiblingLockedAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _tagLockServiceMock.Setup(s => s.ToggleTagLockAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        IRenderedComponent<TagDetail> cut = RenderTagDetail(10, "admin-user", "Admin");
        cut.WaitForState(() => cut.Markup.Contains("子タグ"));

        // First switch is "このTagをロック"
        var switches = cut.FindAll("input[type='checkbox']");
        var tagLockSwitch = switches[0];
        await cut.InvokeAsync(() => tagLockSwitch.Change(true));

        _tagLockServiceMock.Verify(s => s.ToggleTagLockAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenAdminTogglesAncestorsLock_LockAncestorsIsCalled()
    {
        var tag = new TagEntity
        {
            Id = 10,
            Name = "子タグ",
            OwnerId = "tag-owner",
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        var pageData = new TagDetailPageData(tag, false, [], [], [], [], []);
        _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(10, "admin-user")).ReturnsAsync(pageData);
        _tagLockServiceMock.Setup(s => s.AreAncestorsLockedAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _tagLockServiceMock.Setup(s => s.IsTagOrSiblingLockedAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        IRenderedComponent<TagDetail> cut = RenderTagDetail(10, "admin-user", "Admin");
        cut.WaitForState(() => cut.Markup.Contains("子タグ"));

        // Second switch is "このTagの祖先をロック"
        var switches = cut.FindAll("input[type='checkbox']");
        var ancestorLockSwitch = switches[1];
        await cut.InvokeAsync(() => ancestorLockSwitch.Change(true));

        _tagLockServiceMock.Verify(s => s.LockAncestorsAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}