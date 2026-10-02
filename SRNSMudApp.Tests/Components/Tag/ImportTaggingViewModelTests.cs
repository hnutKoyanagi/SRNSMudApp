#region

using System.Security.Claims;

using Microsoft.JSInterop;

using Moq;

using MudBlazor;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;

using Xunit;

using TagEntity = SRNSMudApp.Data.Tag;

#endregion

namespace SRNSMudApp.Tests.Components.Tag;

public class ImportTaggingViewModelTests
{
    private readonly Mock<ITaggingImportDataProvider> _dataProviderMock = new();
    private readonly Mock<ITagHierarchyService> _hierarchyServiceMock = new();
    private readonly Mock<IDialogLauncher> _dialogLauncherMock = new();
    private readonly Mock<IJSRuntime> _jsRuntimeMock = new();

    private ImportTaggingViewModel CreateSut()
    {
        return new ImportTaggingViewModel(
            _dataProviderMock.Object,
            _hierarchyServiceMock.Object,
            _dialogLauncherMock.Object);
    }

    [Fact]
    public void Constructor_WhenDependenciesNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImportTaggingViewModel(null!, _hierarchyServiceMock.Object, _dialogLauncherMock.Object));
        Assert.Throws<ArgumentNullException>(() => new ImportTaggingViewModel(_dataProviderMock.Object, null!, _dialogLauncherMock.Object));
        Assert.Throws<ArgumentNullException>(() => new ImportTaggingViewModel(_dataProviderMock.Object, _hierarchyServiceMock.Object, null!));
    }

    [Fact]
    public void ParsePayload_WhenJsonEmpty_ReturnsFailure()
    {
        var result = ImportTaggingViewModel.ParsePayload("   ");
        Assert.True(result is Failure f && f.ErrorMessage == "JSONテキストが空です。");
    }

    [Fact]
    public void ParsePayload_WhenJsonMalformed_ReturnsFailure()
    {
        var result = ImportTaggingViewModel.ParsePayload("{ invalid json");
        Assert.True(result is Failure f && f.ErrorMessage.Contains("JSONの解析に失敗しました"));
    }

    [Fact]
    public void ParsePayload_WhenMissingTaggingRequestEntity_ReturnsFailure()
    {
        var result = ImportTaggingViewModel.ParsePayload("{\"other\": 123}");
        Assert.True(result is Failure f && f.ErrorMessage == "有効な TaggingRequestEntity が含まれていません。");
    }

    [Fact]
    public void ParsePayload_WhenValid_ReturnsParsedPayload()
    {
        const string validJson = """
        {
          "taggingRequestEntity": {
            "item": [{ "title": "Test", "content": "Text" }],
            "tagRelations": [],
            "tagEdges": []
          }
        }
        """;

        var result = ImportTaggingViewModel.ParsePayload(validJson);
        Assert.True(result is Success<TaggingImportPayload> s && s.Value.Item.Count == 1);
    }

    [Fact]
    public void ReplaceLink_WhenDecisionReplace_ReplacesWithTagDetailUri()
    {
        const string content = "This is [Dog](http://example.com/q=rel-001) item.";
        const string match = "[Dog](http://example.com/q=rel-001)";
        var decision = new TagLinkReplaceDecision(TagLinkReplaceAction.ReplaceWithFirstCandidate, SelectedTagId: 42);

        var actual = ImportTaggingViewModel.ReplaceLink(content, match, "Dog", decision);

        Assert.Equal("This is /TagDetail/42 item.", actual);
    }

    [Fact]
    public void ReplaceLink_WhenDecisionDoNotReplace_RemovesLinkMarkupKeepingLabelOnly()
    {
        const string content = "This is [Dog](http://example.com/q=rel-001) item.";
        const string match = "[Dog](http://example.com/q=rel-001)";
        var decision = new TagLinkReplaceDecision(TagLinkReplaceAction.DoNotReplace, SelectedTagId: null);

        var actual = ImportTaggingViewModel.ReplaceLink(content, match, "Dog", decision);

        Assert.Equal("This is Dog item.", actual);
    }

    [Fact]
    public void ReplaceLink_WhenDecisionNull_RemovesLinkMarkupKeepingLabelOnly()
    {
        const string content = "This is [Dog](http://example.com/q=rel-001) item.";
        const string match = "[Dog](http://example.com/q=rel-001)";

        var actual = ImportTaggingViewModel.ReplaceLink(content, match, "Dog", null);

        Assert.Equal("This is Dog item.", actual);
    }

    [Fact]
    public async Task ExecuteImportAsync_WhenUserNull_ReturnsFailure()
    {
        var sut = CreateSut();
        sut.JsonText = "{}";

        var result = await sut.ExecuteImportAsync(_jsRuntimeMock.Object);

        Assert.True(result is Failure f && f.ErrorMessage == "ログインが必要です。");
    }

    [Fact]
    public async Task ExecuteImportAsync_WhenExistingTag_ExecutesImportSuccessfully()
    {
        var sut = CreateSut();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user-123") };
        sut.Initialize(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));

        const string validJson = """
        {
          "taggingRequestEntity": {
            "item": [{ "title": "Item1", "content": "Hello [Tag1](http://rel1)" }],
            "tagRelations": [
              {
                "relationId": "rel1",
                "tag": { "name": "Tag1", "tagKind": "UserCustomTag" }
              }
            ],
            "tagEdges": []
          }
        }
        """;
        sut.JsonText = validJson;

        var existingTag = new TagEntity { Id = 10, Name = "Tag1", OwnerId = "user-123" };
        _dataProviderMock
            .Setup(d => d.FindExistingTagAsync("Tag1", "user-123", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTag);

        _dataProviderMock
            .Setup(d => d.SearchTagsAsync("Tag1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingTag]);

        // リンク置換ダイアログモック
        var dialogResultMock = new Mock<IDialogReference>();
        dialogResultMock
            .Setup(d => d.Result)
            .ReturnsAsync(DialogResult.Ok(new TagLinkReplaceDecision(TagLinkReplaceAction.ReplaceWithFirstCandidate, 10)));

        _dialogLauncherMock
            .Setup(l => l.ShowAsync(typeof(TagLinkReplaceDialog), It.IsAny<string>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogResultMock.Object);

        var expectedImportResult = new TaggingImportResult(1, 1, 0, 0, 0, []);
        _dataProviderMock
            .Setup(d => d.ExecuteImportAsync("user-123", It.IsAny<TaggingImportPayload>(), It.IsAny<IReadOnlyDictionary<string, int>>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedImportResult);

        var result = await sut.ExecuteImportAsync(_jsRuntimeMock.Object);

        Assert.True(result is Success<TaggingImportResult> s && s.Value == expectedImportResult);
        Assert.False(sut.IsProcessing);
        Assert.Same(expectedImportResult, sut.ImportResult);
    }

    [Fact]
    public async Task ExecuteImportAsync_WhenProviderThrows_ReturnsFailure()
    {
        var sut = CreateSut();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user-123") };
        sut.Initialize(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));

        const string validJson = """
        {
          "taggingRequestEntity": {
            "item": [{ "title": "Item1", "content": "Text" }],
            "tagRelations": [],
            "tagEdges": []
          }
        }
        """;
        sut.JsonText = validJson;

        _dataProviderMock
            .Setup(d => d.ExecuteImportAsync(It.IsAny<string>(), It.IsAny<TaggingImportPayload>(), It.IsAny<IReadOnlyDictionary<string, int>>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB timeout"));

        var result = await sut.ExecuteImportAsync(_jsRuntimeMock.Object);

        Assert.True(result is Failure f && f.ErrorMessage.Contains("DB timeout"));
        Assert.False(sut.IsProcessing);
    }
}