using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

using TagEntity = SRNSMudApp.Data.Tag;
using UserGroupEntity = SRNSMudApp.Data.UserGroup;

namespace SRNSMudApp.Tests.Components.Tag;

/// <summary>
/// <see cref="TagEditDialog"/> の描画、プライベート設定・グループ選択、保存およびロック時エラーハンドリングを検証する bUnit テスト。
/// </summary>
public sealed class TagEditDialogTests : IAsyncLifetime
{
    private const string OwnerUserId = "owner-user-id";

    private readonly BunitContext _ctx = new();
    private readonly Mock<ITagCommandService> _tagCommandMock = new();
    private readonly Mock<IUserGroupDataProvider> _userGroupDataMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();

    public TagEditDialogTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _tagCommandMock.Object);
        _ = _ctx.Services.AddScoped(_ => _userGroupDataMock.Object);
        _ = _ctx.Services.AddScoped(_ => _snackbarMock.Object);
        _ = _ctx.Services.AddAuthorizationCore();
        _ = _ctx.Services.AddAuth(OwnerUserId);
        _ = _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    [Fact]
    public async Task Render_PopulatesExistingTagFields_AndDisablesPrivateSwitchForSystemTag()
    {
        // Arrange
        var systemTag = new TagEntity
        {
            Id = 5,
            Name = "Good",
            Content = "System vote tag",
            OwnerId = OwnerUserId,
            IsSystem = true
        };
        _ = _userGroupDataMock
            .Setup(d => d.GetManagedGroupsAsync(OwnerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<TagEditDialog>
        {
            { x => x.Tag, systemTag }
        };

        // Act
        _ = await dialogService.ShowAsync<TagEditDialog>("タグを編集", parameters);

        // Assert
        host.WaitForState(() => host.Markup.Contains("Good"));
        Assert.Contains("System vote tag", host.Markup);

        IRenderedComponent<MudSwitch<bool>> privateSwitch = host.FindComponent<MudSwitch<bool>>();
        Assert.True(privateSwitch.Instance.Disabled);
    }

    [Fact]
    public async Task Save_WhenNameCleared_DisablesSaveButton()
    {
        // Arrange
        var tag = new TagEntity
        {
            Id = 10,
            Name = "InitialTag",
            Content = "InitialContent",
            OwnerId = OwnerUserId
        };
        _ = _userGroupDataMock
            .Setup(d => d.GetManagedGroupsAsync(OwnerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<TagEditDialog>
        {
            { x => x.Tag, tag }
        };

        _ = await dialogService.ShowAsync<TagEditDialog>("タグを編集", parameters);
        host.WaitForState(() => host.FindComponents<MudTextField<string>>().Count >= 2);

        // Act: タグ名を空にする
        IRenderedComponent<MudTextField<string>> nameField = host.FindComponents<MudTextField<string>>()[0];
        await host.InvokeAsync(() => nameField.Instance.ValueChanged.InvokeAsync("   "));

        // Assert
        IElement saveButton = host.FindAll("button").First(b => b.TextContent.Trim() == "保存");
        Assert.True(saveButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task Save_WhenValid_CallsUpdateTagAsyncAndClosesDialog()
    {
        // Arrange
        var tag = new TagEntity
        {
            Id = 12,
            Name = "BeforeName",
            Content = "BeforeContent",
            OwnerId = OwnerUserId,
            AutoAcceptIncomingTaggingRequests = true
        };
        var group = new UserGroupEntity { Id = 3, Name = "TrustedGroup", OwnerId = OwnerUserId };

        _ = _userGroupDataMock
            .Setup(d => d.GetManagedGroupsAsync(OwnerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([group]);
        _ = _tagCommandMock
            .Setup(s => s.UpdateTagAsync(
                12,
                "AfterName",
                "AfterContent",
                true,
                It.IsAny<IEnumerable<int>?>(),
                false,
                true,
                3))
            .ReturnsAsync(true);

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<TagEditDialog>
        {
            { x => x.Tag, tag }
        };

        IDialogReference dialogRef = await dialogService.ShowAsync<TagEditDialog>("タグを編集", parameters);
        host.WaitForState(() => host.FindComponents<MudTextField<string>>().Count >= 2);

        // Act
        var textFields = host.FindComponents<MudTextField<string>>();
        await host.InvokeAsync(() => textFields[0].Instance.ValueChanged.InvokeAsync("AfterName"));
        await host.InvokeAsync(() => textFields[1].Instance.ValueChanged.InvokeAsync("AfterContent"));

        IRenderedComponent<MudSwitch<bool>> privateSwitch = host.FindComponent<MudSwitch<bool>>();
        await host.InvokeAsync(() => privateSwitch.Instance.ValueChanged.InvokeAsync(true));

        IRenderedComponent<MudSelect<int?>> targetGroupSelect = host.FindComponent<MudSelect<int?>>();
        await host.InvokeAsync(() => targetGroupSelect.Instance.ValueChanged.InvokeAsync(3));

        IElement saveButton = host.FindAll("button").First(b => b.TextContent.Trim() == "保存");
        await host.InvokeAsync(() => saveButton.Click());

        // Assert
        DialogResult? result = await dialogRef.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
        Assert.False(result.Canceled);
        _tagCommandMock.Verify(
            s => s.UpdateTagAsync(
                12,
                "AfterName",
                "AfterContent",
                true,
                It.IsAny<IEnumerable<int>?>(),
                false,
                true,
                3),
            Times.Once);
    }

    [Fact]
    public async Task Save_WhenUpdateReturnsFalse_ShowsWarningSnackbarAndCancelsDialog()
    {
        // Arrange
        var tag = new TagEntity
        {
            Id = 15,
            Name = "LockedTag",
            Content = "Content",
            OwnerId = OwnerUserId
        };
        _ = _userGroupDataMock
            .Setup(d => d.GetManagedGroupsAsync(OwnerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _ = _tagCommandMock
            .Setup(s => s.UpdateTagAsync(
                15,
                "LockedTag",
                "Content",
                false,
                It.IsAny<IEnumerable<int>?>(),
                false,
                false,
                null))
            .ReturnsAsync(false);

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<TagEditDialog>
        {
            { x => x.Tag, tag }
        };

        IDialogReference dialogRef = await dialogService.ShowAsync<TagEditDialog>("タグを編集", parameters);
        host.WaitForState(() => host.Markup.Contains("保存"));

        // Act
        IElement saveButton = host.FindAll("button").First(b => b.TextContent.Trim() == "保存");
        await host.InvokeAsync(() => saveButton.Click());

        // Assert
        DialogResult? result = await dialogRef.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
        Assert.True(result.Canceled);
        _snackbarMock.Verify(
            s => s.Add(
                It.Is<string>(msg => msg.Contains("ロックされているため更新できません")),
                Severity.Warning,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string>()),
            Times.Once);
    }

    private sealed class DialogHost : ComponentBase
    {
        [Parameter] public RenderFragment ChildContent { get; set; } = _ => { };

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, nameof(CascadingAuthenticationState.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<MudDialogProvider>(0);
                b.CloseComponent();
                b.AddContent(1, ChildContent);
            }));
            builder.CloseComponent();
        }
    }
}