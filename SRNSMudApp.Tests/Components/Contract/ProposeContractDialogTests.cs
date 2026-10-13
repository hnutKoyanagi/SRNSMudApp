using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Contract;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;
using SRNSMudApp.Tests.TestSupport;

using Item = SRNSMudApp.Data.Item;
using Tag = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Tests.Components.Contract;

public sealed class ProposeContractDialogTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly Mock<IContractLookupDataProvider> _contractDataMock = new();
    private readonly Mock<ITaggingContractService> _contractServiceMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();

    public ProposeContractDialogTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices();
        _ = _ctx.Services.AddScoped(_ => _contractDataMock.Object);
        _ = _ctx.Services.AddScoped(_ => _contractServiceMock.Object);
        _ = _ctx.Services.AddScoped(_ => new ProposeContractViewModel(_contractServiceMock.Object, _contractDataMock.Object));
        _ = _ctx.Services.AddScoped(_ => _snackbarMock.Object);
        _ = _ctx.Services.AddAuthorizationCore();
        _ = _ctx.Services.AddAuth("user-1");
        _ = _ctx.Render<MudPopoverProvider>();

        _contractDataMock.Setup(d => d.GetAvailableRightAssetsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);
        _contractServiceMock.Setup(s => s.ProposeGratisContractAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<TaggingRequestType>(), It.IsAny<int>(), It.IsAny<string?>()))
            .ReturnsAsync(new Success<TaggingRequestEntity>(new TaggingRequestEntity { OwnerId = "user-1" }));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public async Task WhenTargetItemIsProvided_DisplaysReadOnlyItemTextField()
    {
        var targetItem = new SRNSMudApp.Data.Item { Id = 10, Content = "テスト対象アイテム", OwnerId = "user-1" };
        var requestedTag = new SRNSMudApp.Data.Tag { Id = 20, Name = "テストタグ", OwnerId = "tag-owner" };

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ProposeContractDialog>
        {
            { x => x.TargetItem, targetItem },
            { x => x.RequestedTag, requestedTag }
        };

        _ = await dialogService.ShowAsync<ProposeContractDialog>("コントラクトの提案", parameters);

        host.WaitForState(() => host.Markup.Contains("テスト対象アイテム"));

        // MudTextField で表示され、MudAutocomplete は存在しないこと
        Assert.Contains("テスト対象アイテム", host.Markup);
        Assert.Empty(host.FindComponents<MudAutocomplete<SRNSMudApp.Data.Item>>());
    }

    [Fact]
    public async Task WhenTargetItemIsNull_DisplaysItemAutocomplete()
    {
        var requestedTag = new SRNSMudApp.Data.Tag { Id = 20, Name = "テストタグ", OwnerId = "tag-owner" };

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ProposeContractDialog>
        {
            { x => x.RequestedTag, requestedTag }
        };

        _ = await dialogService.ShowAsync<ProposeContractDialog>("コントラクトの提案", parameters);

        host.WaitForState(() => host.Markup.Contains("対象のアイテム"));

        // MudAutocomplete が存在すること
        Assert.NotEmpty(host.FindComponents<MudAutocomplete<SRNSMudApp.Data.Item>>());
    }

    [Fact]
    public async Task WhenTargetItemIsNull_SubmittingWithoutSelection_ShowsErrorMessage()
    {
        var requestedTag = new SRNSMudApp.Data.Tag { Id = 20, Name = "テストタグ", OwnerId = "tag-owner" };

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ProposeContractDialog>
        {
            { x => x.RequestedTag, requestedTag }
        };

        _ = await dialogService.ShowAsync<ProposeContractDialog>("コントラクトの提案", parameters);

        host.WaitForState(() => host.Markup.Contains("提案する"));

        IElement submitButton = host.FindAll("button").First(b => b.TextContent.Trim() == "提案する");
        submitButton.Click();

        _snackbarMock.Verify(s => s.Add("対象のアイテムを選択してください。", Severity.Error, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Once);
        _contractServiceMock.Verify(s => s.ProposeGratisContractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<TaggingRequestType>(), It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task WhenTargetItemIsNull_SelectingItemAndSubmitting_ProposesGratisContractSuccessfully()
    {
        var requestedTag = new SRNSMudApp.Data.Tag { Id = 20, Name = "テストタグ", OwnerId = "tag-owner" };
        var selectedItem = new SRNSMudApp.Data.Item { Id = 30, Content = "選択したアイテム", OwnerId = "user-1" };

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ProposeContractDialog>
        {
            { x => x.RequestedTag, requestedTag }
        };

        IDialogReference dialog = await dialogService.ShowAsync<ProposeContractDialog>("コントラクトの提案", parameters);

        host.WaitForState(() => host.FindComponents<MudAutocomplete<SRNSMudApp.Data.Item>>().Count > 0);

        IRenderedComponent<MudAutocomplete<SRNSMudApp.Data.Item>> autocomplete =
            host.FindComponent<MudAutocomplete<SRNSMudApp.Data.Item>>();
        await host.InvokeAsync(() => autocomplete.Instance.ValueChanged.InvokeAsync(selectedItem));

        IElement submitButton = host.FindAll("button").First(b => b.TextContent.Trim() == "提案する");
        await host.InvokeAsync(() => submitButton.Click());

        _contractServiceMock.Verify(s => s.ProposeGratisContractAsync(
            "user-1",
            "tag-owner",
            30,
            20,
            TaggingRequestType.Add,
            1,
            It.IsAny<string?>()), Times.Once);

        DialogResult? result = await dialog.Result;
        Assert.NotNull(result);
        Assert.False(result.Canceled);
    }

    [Fact]
    public async Task WhenMutualTabSelected_SubmittingWithoutOfferedFields_ShowsValidationError()
    {
        var targetItem = new SRNSMudApp.Data.Item { Id = 10, Content = "テスト対象アイテム", OwnerId = "user-1" };
        var requestedTag = new SRNSMudApp.Data.Tag { Id = 20, Name = "テストタグ", OwnerId = "tag-owner" };

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ProposeContractDialog>
        {
            { x => x.TargetItem, targetItem },
            { x => x.RequestedTag, requestedTag }
        };

        _ = await dialogService.ShowAsync<ProposeContractDialog>("コントラクトの提案", parameters);

        host.WaitForState(() => host.Markup.Contains("相互タグ付け (Mutual)"));

        IRenderedComponent<MudTabs> tabs = host.FindComponent<MudTabs>();
        await host.InvokeAsync(() => tabs.Instance.ActivatePanelAsync(1));

        IElement submitButton = host.FindAll("button").First(b => b.TextContent.Trim() == "提案する");
        await host.InvokeAsync(() => submitButton.Click());

        _snackbarMock.Verify(
            s => s.Add("相互タグ付けに必要な項目を入力してください。", Severity.Error, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()),
            Times.Once);
        _contractServiceMock.Verify(
            s => s.ProposeMutualContractAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<TaggingRequestType>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task WhenMutualTabSelected_WithAllRequiredFields_ProposesMutualContractSuccessfully()
    {
        var targetItem = new SRNSMudApp.Data.Item { Id = 10, Content = "テスト対象アイテム", OwnerId = "user-1" };
        var requestedTag = new SRNSMudApp.Data.Tag { Id = 20, Name = "テストタグ", OwnerId = "tag-owner" };
        var offeredItem = new SRNSMudApp.Data.Item { Id = 40, Content = "提供アイテム", OwnerId = "tag-owner" };
        var offeredTag = new SRNSMudApp.Data.Tag { Id = 50, Name = "提供タグ", OwnerId = "user-1" };
        var asset = new RightAsset { Id = 60, OwnerId = "user-1", TargetTagId = 50, TargetTag = offeredTag, Amount = 1 };

        _contractDataMock.Setup(d => d.GetAvailableRightAssetsAsync("user-1"))
            .ReturnsAsync([asset]);
        _contractServiceMock.Setup(s => s.ProposeMutualContractAsync(
                "user-1", "tag-owner", 10, 20, 40, 50, 60, TaggingRequestType.Add, 1))
            .ReturnsAsync(new Success<TaggingRequestEntity>(new TaggingRequestEntity { OwnerId = "user-1" }));

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ProposeContractDialog>
        {
            { x => x.TargetItem, targetItem },
            { x => x.RequestedTag, requestedTag }
        };

        IDialogReference dialog = await dialogService.ShowAsync<ProposeContractDialog>("コントラクトの提案", parameters);

        host.WaitForState(() => host.Markup.Contains("相互タグ付け (Mutual)"));

        IRenderedComponent<MudTabs> tabs = host.FindComponent<MudTabs>();
        await host.InvokeAsync(() => tabs.Instance.ActivatePanelAsync(1));

        IRenderedComponent<MudAutocomplete<SRNSMudApp.Data.Item>> itemAutocomplete =
            host.FindComponent<MudAutocomplete<SRNSMudApp.Data.Item>>();
        await host.InvokeAsync(() => itemAutocomplete.Instance.ValueChanged.InvokeAsync(offeredItem));

        IRenderedComponent<MudAutocomplete<SRNSMudApp.Data.Tag>> tagAutocomplete =
            host.FindComponent<MudAutocomplete<SRNSMudApp.Data.Tag>>();
        await host.InvokeAsync(() => tagAutocomplete.Instance.ValueChanged.InvokeAsync(offeredTag));

        IRenderedComponent<MudSelect<RightAsset>> assetSelect =
            host.FindComponent<MudSelect<RightAsset>>();
        await host.InvokeAsync(() => assetSelect.Instance.ValueChanged.InvokeAsync(asset));

        IElement submitButton = host.FindAll("button").First(b => b.TextContent.Trim() == "提案する");
        await host.InvokeAsync(() => submitButton.Click());

        _contractServiceMock.Verify(
            s => s.ProposeMutualContractAsync("user-1", "tag-owner", 10, 20, 40, 50, 60, TaggingRequestType.Add, 1),
            Times.Once);

        DialogResult? result = await dialog.Result;
        Assert.NotNull(result);
        Assert.False(result.Canceled);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
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