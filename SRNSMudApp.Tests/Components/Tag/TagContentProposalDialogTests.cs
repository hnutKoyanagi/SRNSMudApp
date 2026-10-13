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
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using TagEntity = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Tests.Components.Tag;

/// <summary>
/// <see cref="TagContentProposalDialog"/> の描画、提案送信、およびエラーハンドリングを検証する bUnit テスト。
/// </summary>
public sealed class TagContentProposalDialogTests : IAsyncLifetime
{
    private const string ProposerUserId = "proposer-user";

    private readonly BunitContext _ctx = new();
    private readonly Mock<ITagContentProposalService> _proposalServiceMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();

    public TagContentProposalDialogTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _proposalServiceMock.Object);
        _ = _ctx.Services.AddScoped(_ => _snackbarMock.Object);
        _ = _ctx.Services.AddAuthorizationCore();
        _ = _ctx.Services.AddAuth(ProposerUserId);
        _ = _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    [Fact]
    public async Task Render_DisplaysCurrentTagNameAndContent()
    {
        // Arrange
        var tag = new TagEntity
        {
            Id = 10,
            Name = "Blazor",
            Content = "Existing description",
            OwnerId = "owner-user"
        };

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<TagContentProposalDialog>
        {
            { x => x.Tag, tag }
        };

        // Act
        _ = await dialogService.ShowAsync<TagContentProposalDialog>("タグ内容の編集提案", parameters);

        // Assert
        host.WaitForState(() => host.Markup.Contains("Blazor"));
        Assert.Contains("Existing description", host.Markup);
        Assert.Contains("提案を送信", host.Markup);
    }

    [Fact]
    public async Task Submit_WithValidContentAndReason_CallsProposeContentAsyncAndClosesDialog()
    {
        // Arrange
        var tag = new TagEntity
        {
            Id = 10,
            Name = "Blazor",
            Content = "Old description",
            OwnerId = "owner-user"
        };
        var proposal = new TagContentProposal
        {
            Id = 1,
            TagId = 10,
            OwnerId = ProposerUserId,
            RequesterUserId = ProposerUserId,
            ProposedContent = "Updated description",
            Reason = "More accurate"
        };

        _ = _proposalServiceMock
            .Setup(s => s.ProposeContentAsync(
                10,
                "Updated description",
                "More accurate",
                ProposerUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<TagContentProposal>(proposal));

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<TagContentProposalDialog>
        {
            { x => x.Tag, tag }
        };

        IDialogReference dialogRef = await dialogService.ShowAsync<TagContentProposalDialog>("タグ内容の編集提案", parameters);
        host.WaitForState(() => host.FindComponents<MudTextField<string>>().Count >= 3);

        // Act: [0]=現在の内容(ReadOnly), [1]=提案する内容, [2]=提案理由
        var textFields = host.FindComponents<MudTextField<string>>();
        await host.InvokeAsync(() => textFields[1].Instance.ValueChanged.InvokeAsync("Updated description"));
        await host.InvokeAsync(() => textFields[2].Instance.ValueChanged.InvokeAsync("More accurate"));

        IElement submitBtn = host.FindAll("button").First(b => b.TextContent.Contains("提案を送信"));
        await host.InvokeAsync(() => submitBtn.Click());

        // Assert
        DialogResult? result = await dialogRef.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
        Assert.False(result.Canceled);
        _proposalServiceMock.Verify(
            s => s.ProposeContentAsync(
                10,
                "Updated description",
                "More accurate",
                ProposerUserId,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _snackbarMock.Verify(
            s => s.Add("編集提案を送信しました。", Severity.Success, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Submit_WhenServiceReturnsFailure_ShowsErrorSnackbarAndDoesNotClose()
    {
        // Arrange
        var tag = new TagEntity
        {
            Id = 10,
            Name = "Blazor",
            Content = "Old description",
            OwnerId = "owner-user"
        };

        _ = _proposalServiceMock
            .Setup(s => s.ProposeContentAsync(
                10,
                It.IsAny<string>(),
                It.IsAny<string?>(),
                ProposerUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure("同じ内容の提案はできません。"));

        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<TagContentProposalDialog>
        {
            { x => x.Tag, tag }
        };

        _ = await dialogService.ShowAsync<TagContentProposalDialog>("タグ内容の編集提案", parameters);
        host.WaitForState(() => host.Markup.Contains("提案を送信"));

        // Act
        IElement submitBtn = host.FindAll("button").First(b => b.TextContent.Contains("提案を送信"));
        await host.InvokeAsync(() => submitBtn.Click());

        // Assert
        _snackbarMock.Verify(
            s => s.Add("同じ内容の提案はできません。", Severity.Error, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()),
            Times.Once);
        Assert.Contains("提案を送信", host.Markup);
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