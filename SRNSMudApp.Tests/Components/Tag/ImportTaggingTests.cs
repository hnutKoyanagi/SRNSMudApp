using System.Text.Json;

using Bunit;

using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;

namespace SRNSMudApp.Tests.Components.Tag;

public sealed class ImportTaggingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly Mock<ITaggingImportDataProvider> _dataProviderMock = new();
    private readonly Mock<ITagHierarchyService> _hierarchyServiceMock = new();
    private readonly Mock<IDialogLauncher> _dialogLauncherMock = new();

    public ImportTaggingTests()
    {
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _dataProviderMock.Object);
        _ = _ctx.Services.AddScoped(_ => _hierarchyServiceMock.Object);
        _ = _ctx.Services.AddScoped(_ => _dialogLauncherMock.Object);
        _ = _ctx.Services.AddAuth("testuser");
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public void ImportButton_ShouldBeDisabled_WhenJsonIsEmpty()
    {
        var authStateTask = Task.FromResult(BunitTestSetup.CreateAuthState("testuser"));
        IRenderedComponent<ImportTagging> component = _ctx.Render<ImportTagging>(p => p.AddCascadingValue(authStateTask));

        IReadOnlyList<IRenderedComponent<MudButton>> buttons = component.FindComponents<MudButton>();
        IRenderedComponent<MudButton>? startButton = buttons.FirstOrDefault(b => b.Instance.Color == Color.Success);

        Assert.NotNull(startButton);
        Assert.True(startButton.Instance.Disabled);
    }

    [Fact]
    public async Task StartImport_WhenValidJsonProvided_ExecutesValidationAndImport()
    {
        var authStateTask = Task.FromResult(BunitTestSetup.CreateAuthState("testuser"));
        IRenderedComponent<ImportTagging> component = _ctx.Render<ImportTagging>(p => p.AddCascadingValue(authStateTask));

        const string validJson = """
        {
          "TaggingRequestEntity": {
            "Item": [
              {
                "ItemId": "item-001",
                "SequenceOrder": 1,
                "Content": "これは[テスト](https://www.google.com/search?q=rel-001)です。"
              }
            ],
            "TagRelations": [
              {
                "RelationId": "rel-001",
                "SourceItemId": "item-001",
                "Tag": {
                  "Name": "テスト",
                  "TagKind": "UserCustomTag"
                }
              }
            ],
            "TagEdges": []
          }
        }
        """;

        var existingTag = new SRNSMudApp.Data.Tag
        {
            Id = 42,
            Name = "テスト",
            OwnerId = "testuser"
        };

        _ = _dataProviderMock
            .Setup(d => d.FindExistingTagAsync("テスト", "testuser", false, default))
            .ReturnsAsync(existingTag);

        _ = _dataProviderMock
            .Setup(d => d.SearchTagsAsync(It.IsAny<string?>(), default))
            .ReturnsAsync([existingTag]);

        var dialogRefMock = new Mock<IDialogReference>();
        _ = dialogRefMock
            .Setup(d => d.Result)
            .ReturnsAsync(DialogResult.Ok(new TagLinkReplaceDecision(TagLinkReplaceAction.ReplaceWithFirstCandidate, 42)));

        _ = _dialogLauncherMock
            .Setup(l => l.ShowAsync(typeof(TagLinkReplaceDialog), It.IsAny<string>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogRefMock.Object);

        _ = _dataProviderMock
            .Setup(d => d.ExecuteImportAsync(
                "testuser",
                It.IsAny<TaggingImportPayload>(),
                It.IsAny<IReadOnlyDictionary<string, int>>(),
                It.IsAny<IReadOnlyList<string>>(),
                default))
            .ReturnsAsync(new TaggingImportResult(1, 0, 1, 0, 0, ["成功"]));

        // JSONテキストを入力
        IRenderedComponent<MudTextField<string>> textField = component.FindComponent<MudTextField<string>>();
        await component.InvokeAsync(() => textField.Instance.ValueChanged.InvokeAsync(validJson));

        component.Render();

        IReadOnlyList<IRenderedComponent<MudButton>> buttons = component.FindComponents<MudButton>();
        IRenderedComponent<MudButton>? startButton = buttons.FirstOrDefault(b => b.Instance.Color == Color.Success);
        Assert.NotNull(startButton);
        Assert.False(startButton.Instance.Disabled);

        // インポートボタンをクリック
        await component.InvokeAsync(() => startButton.Find("button").Click());

        // ExecuteImportAsync が呼ばれたことを検証: []() が消去され内部リンクURL (/TagDetail/42) が挿入されていること
        _dataProviderMock.Verify(d => d.ExecuteImportAsync(
            "testuser",
            It.IsAny<TaggingImportPayload>(),
            It.IsAny<IReadOnlyDictionary<string, int>>(),
            It.Is<IReadOnlyList<string>>(contents => contents.Any(c => c.Contains("/TagDetail/42") && !c.Contains("[テスト]"))),
            default), Times.Once);
    }

    [Fact]
    public async Task StartImport_WhenDoNotReplaceChosen_RetainsExistingUrlWithoutBrackets()
    {
        var authStateTask = Task.FromResult(BunitTestSetup.CreateAuthState("testuser"));
        IRenderedComponent<ImportTagging> component = _ctx.Render<ImportTagging>(p => p.AddCascadingValue(authStateTask));

        const string validJson = """
        {
          "TaggingRequestEntity": {
            "Item": [
              {
                "ItemId": "item-001",
                "SequenceOrder": 1,
                "Content": "これは[テスト](https://www.google.com/search?q=rel-001)です。"
              }
            ],
            "TagRelations": [
              {
                "RelationId": "rel-001",
                "SourceItemId": "item-001",
                "Tag": {
                  "Name": "テスト",
                  "TagKind": "UserCustomTag"
                }
              }
            ],
            "TagEdges": []
          }
        }
        """;

        var existingTag = new SRNSMudApp.Data.Tag
        {
            Id = 42,
            Name = "テスト",
            OwnerId = "testuser"
        };

        _ = _dataProviderMock
            .Setup(d => d.FindExistingTagAsync("テスト", "testuser", false, default))
            .ReturnsAsync(existingTag);

        _ = _dataProviderMock
            .Setup(d => d.SearchTagsAsync(It.IsAny<string?>(), default))
            .ReturnsAsync([existingTag]);

        var dialogRefMock = new Mock<IDialogReference>();
        _ = dialogRefMock
            .Setup(d => d.Result)
            .ReturnsAsync(DialogResult.Ok(new TagLinkReplaceDecision(TagLinkReplaceAction.DoNotReplace, null)));

        _ = _dialogLauncherMock
            .Setup(l => l.ShowAsync(typeof(TagLinkReplaceDialog), It.IsAny<string>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogRefMock.Object);

        _ = _dataProviderMock
            .Setup(d => d.ExecuteImportAsync(
                "testuser",
                It.IsAny<TaggingImportPayload>(),
                It.IsAny<IReadOnlyDictionary<string, int>>(),
                It.IsAny<IReadOnlyList<string>>(),
                default))
            .ReturnsAsync(new TaggingImportResult(1, 0, 1, 0, 0, ["成功"]));

        IRenderedComponent<MudTextField<string>> textField = component.FindComponent<MudTextField<string>>();
        await component.InvokeAsync(() => textField.Instance.ValueChanged.InvokeAsync(validJson));
        component.Render();

        IReadOnlyList<IRenderedComponent<MudButton>> buttons = component.FindComponents<MudButton>();
        IRenderedComponent<MudButton>? startButton = buttons.FirstOrDefault(b => b.Instance.Color == Color.Success);
        Assert.NotNull(startButton);

        await component.InvokeAsync(() => startButton.Find("button").Click());

        // 置き換えなかった時は URL と記号 ([]()) が削除され、ラベルの文字のみが残ること（例: "これはテストです。"）
        _dataProviderMock.Verify(d => d.ExecuteImportAsync(
            "testuser",
            It.IsAny<TaggingImportPayload>(),
            It.IsAny<IReadOnlyDictionary<string, int>>(),
            It.Is<IReadOnlyList<string>>(contents => contents.Any(c => c == "これはテストです。")),
            default), Times.Once);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}