using Moq;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Tag;

/// <summary>
///     TagTableViewModel の単体テスト。
///     検索フィルタ・添付タグ表示計算・権限判定を bUnit なしで検証する。
/// </summary>
public class TagTableViewModelTests
{
    private static SRNSMudApp.Data.Tag CreateTag(int id = 1, string name = "Tag", string ownerId = "user-1", bool isSystem = false) =>
        new()
        {
            Id = id,
            Name = name,
            OwnerId = ownerId,
            IsSystem = isSystem,
            Content = $"content of {name}"
        };

    private static TagRelationToTag CreateRelation(
        int id, int weight = 1, string ownerId = "user-1", bool isSystemTag = false)
    {
        var tag = CreateTag(id, $"tag-{id}", ownerId, isSystemTag);
        return new TagRelationToTag { Id = id, Weight = weight, OwnerId = ownerId, Tag = tag };
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FilterFunc_WithBlankSearch_MatchesAllTags(string? search)
    {
        var tag = CreateTag();

        Assert.True(TagTableViewModel.FilterFunc(tag, search!));
    }

    [Fact]
    public void FilterFunc_ByTagName_Matches()
    {
        var tag = CreateTag(name: "Server");

        Assert.True(TagTableViewModel.FilterFunc(tag, "serv"));
    }

    [Fact]
    public void FilterFunc_ByContentOrOwnerName_Matches()
    {
        var tag = CreateTag(name: "Server");
        tag.Owner = new ApplicationUser { Id = tag.OwnerId, UserName = "alice" };

        Assert.True(TagTableViewModel.FilterFunc(tag, "content"));
        Assert.True(TagTableViewModel.FilterFunc(tag, "ALICE"));
    }

    [Fact]
    public void FilterFunc_WithNoMatch_ReturnsFalse()
    {
        var tag = CreateTag(name: "Server");

        Assert.False(TagTableViewModel.FilterFunc(tag, "database"));
    }

    [Fact]
    public void GetTagSearchSuggestions_WithEmptyValue_ReturnsDistinctNamesLimitedTo20()
    {
        var tags = Enumerable.Range(1, 30)
            .Select(i => CreateTag(i, $"tag-{i % 25}"))
            .ToList();

        var suggestions = TagTableViewModel.GetTagSearchSuggestions(tags, "");

        Assert.Equal(20, suggestions.Count);
        Assert.All(suggestions, Assert.NotNull);
    }

    [Fact]
    public void GetTagSearchSuggestions_WithValue_FiltersByName()
    {
        List<SRNSMudApp.Data.Tag> tags = [CreateTag(1, "server"), CreateTag(2, "service"), CreateTag(3, "database")];

        var suggestions = TagTableViewModel.GetTagSearchSuggestions(tags, "ser");

        Assert.Equal(["server", "service"], suggestions);
    }

    [Fact]
    public void GetAttachedTags_ExcludesSystemTags_AndSortsByWeightDescending()
    {
        var tag = CreateTag();
        tag.TargetTagRelations =
        [
            CreateRelation(1, weight: 1),
            CreateRelation(2, weight: 5),
            CreateRelation(3, weight: 3, isSystemTag: true)
        ];

        var attached = TagTableViewModel.GetAttachedTags(tag);

        Assert.Equal([2, 1], attached.Select(tr => tr.Id));
    }

    [Fact]
    public void GetAttachedTagsDisplay_ForManyTags_CollapsesToLimitWithHiddenCount()
    {
        var tag = CreateTag();
        tag.TargetTagRelations =
        [
            CreateRelation(1, weight: 5),
            CreateRelation(2, weight: 4),
            CreateRelation(3, weight: 3)
        ];

        var display = TagTableViewModel.GetAttachedTagsDisplay(tag, isExpanded: false);

        Assert.True(display.HasManyTags);
        Assert.Equal(1, display.HiddenCount);
        Assert.Equal([1, 2], display.TagsToDisplay.Select(tr => tr.Id));
        Assert.Equal("+1 more", display.ToggleLabel);
    }

    [Fact]
    public void GetAttachedTagsDisplay_WhenExpanded_ShowsAllTags()
    {
        var tag = CreateTag();
        tag.TargetTagRelations =
        [
            CreateRelation(1, weight: 5),
            CreateRelation(2, weight: 4),
            CreateRelation(3, weight: 3)
        ];

        var display = TagTableViewModel.GetAttachedTagsDisplay(tag, isExpanded: true);

        Assert.True(display.HasManyTags);
        Assert.Equal(0, display.HiddenCount);
        Assert.Equal(3, display.TagsToDisplay.Count);
        Assert.Equal("閉じる", display.ToggleLabel);
    }

    [Fact]
    public void GetAttachedTagsDisplay_ForFewTags_ShowsAllWithoutToggle()
    {
        var tag = CreateTag();
        tag.TargetTagRelations = [CreateRelation(1), CreateRelation(2)];

        var display = TagTableViewModel.GetAttachedTagsDisplay(tag, isExpanded: false);

        Assert.False(display.HasManyTags);
        Assert.Equal(0, display.HiddenCount);
        Assert.Equal(2, display.TagsToDisplay.Count);
    }

    [Theory]
    [InlineData("user-1", true)]
    [InlineData("user-2", false)]
    public void CanEditTag_OnlyForOwner(string userId, bool expected) => Assert.Equal(expected, TagTableViewModel.CanEditTag(CreateTag(ownerId: "user-1"), userId));

    [Fact]
    public void CanEditTag_WhenLocked_CannotEditEvenByOwner()
    {
        var tag = CreateTag(ownerId: "user-1");
        Assert.False(TagTableViewModel.CanEditTag(tag, "user-1", isLocked: true));
        Assert.True(TagTableViewModel.CanEditTag(tag, "user-1", isLocked: false));
    }

    [Fact]
    public void CanEditTag_WhenLocked_AdminCanEdit()
    {
        var tag = CreateTag(ownerId: "user-1");
        // 非管理者はオーナーであってもロック時は編集不可
        Assert.False(TagTableViewModel.CanEditTag(tag, "user-1", isLocked: true, isAdmin: false));
        // 管理者はオーナーでなくても、ロック時であっても編集可能
        Assert.True(TagTableViewModel.CanEditTag(tag, "admin-user", isLocked: true, isAdmin: true));
    }

    [Fact]
    public void CanDeleteTag_SystemTagIsNotDeletable_EvenByOwner()
    {
        Assert.False(TagTableViewModel.CanDeleteTag(CreateTag(isSystem: true), "user-1"));
        Assert.True(TagTableViewModel.CanDeleteTag(CreateTag(isSystem: false), "user-1"));
    }

    [Fact]
    public void CanDeleteTag_WhenLocked_CannotDeleteEvenByOwner()
    {
        var tag = CreateTag(ownerId: "user-1", isSystem: false);
        Assert.False(TagTableViewModel.CanDeleteTag(tag, "user-1", isLocked: true));
        Assert.True(TagTableViewModel.CanDeleteTag(tag, "user-1", isLocked: false));
    }

    [Fact]
    public void CanDeleteTag_WhenLocked_AdminCanDeleteNonSystemTag()
    {
        var tag = CreateTag(ownerId: "user-1", isSystem: false);
        // 非管理者はロック時は削除不可
        Assert.False(TagTableViewModel.CanDeleteTag(tag, "user-1", isLocked: true, isAdmin: false));
        // 管理者はロック時であっても非システムタグであれば削除可能
        Assert.True(TagTableViewModel.CanDeleteTag(tag, "admin-user", isLocked: true, isAdmin: true));
        // 管理者であってもシステムタグは削除不可
        Assert.False(TagTableViewModel.CanDeleteTag(CreateTag(ownerId: "user-1", isSystem: true), "admin-user", isLocked: false, isAdmin: true));
    }

    [Theory]
    [InlineData("user-1", true)]
    [InlineData("user-2", false)]
    public void CanRemoveRelation_OnlyForRelationOwner(string userId, bool expected) => Assert.Equal(expected, TagTableViewModel.CanRemoveRelation(CreateRelation(1), userId));

    // --- インスタンスメソッドのテスト ---

    [Fact]
    public async Task InitializeAsync_LoadsAllTagsAndLockStatus()
    {
        var dataMock = new Mock<ITagTableDataProvider>();
        var lockMock = new Mock<ITagLockService>();

        dataMock.Setup(d => d.GetAllTagsAsync())
            .ReturnsAsync([CreateTag(1, "Tag1"), CreateTag(2, "Tag2")]);
        lockMock.Setup(l => l.GetAllTagsWithLockStatusAsync(default))
            .ReturnsAsync([new TagLockItemDto(1, "Tag1", 1, null, null, "u1", true, false, false, true)]);

        var vm = new TagTableViewModel(dataMock.Object, lockMock.Object);
        await vm.InitializeAsync();

        Assert.Equal(2, vm.AllTagsCache.Count);
        Assert.True(vm.IsTagLocked(1));
        Assert.False(vm.IsTagLocked(2));
    }

    [Fact]
    public async Task AddRelationAsync_AlreadyExists_ReturnsWarning()
    {
        var dataMock = new Mock<ITagTableDataProvider>();
        var lockMock = new Mock<ITagLockService>();
        dataMock.Setup(d => d.AddRelationAsync(1, 2, "u1"))
            .ReturnsAsync(TagCardOperationResult.AlreadyExists);

        var vm = new TagTableViewModel(dataMock.Object, lockMock.Object);
        vm.SetUser("u1", false);

        TagCardActionResult result = await vm.AddRelationAsync(1, 2);

        Assert.Equal(TagCardActionResultType.Warning, result.Type);
        Assert.Equal("このタグは既に追加されています。", result.Message);
    }

    [Fact]
    public async Task AddRelationAsync_Success_ReturnsSuccess()
    {
        var dataMock = new Mock<ITagTableDataProvider>();
        var lockMock = new Mock<ITagLockService>();
        dataMock.Setup(d => d.AddRelationAsync(1, 2, "u1"))
            .ReturnsAsync(TagCardOperationResult.Success);

        var vm = new TagTableViewModel(dataMock.Object, lockMock.Object);
        vm.SetUser("u1", false);

        TagCardActionResult result = await vm.AddRelationAsync(1, 2);

        Assert.Equal(TagCardActionResultType.Success, result.Type);
        Assert.Equal("タグを追加しました。", result.Message);
        Assert.True(result.ShouldNotifyChanged);
    }

    [Fact]
    public async Task RemoveRelationAsync_NotOwner_ReturnsError()
    {
        var dataMock = new Mock<ITagTableDataProvider>();
        var lockMock = new Mock<ITagLockService>();

        var vm = new TagTableViewModel(dataMock.Object, lockMock.Object);
        vm.SetUser("u1", false);

        TagCardActionResult result = await vm.RemoveRelationAsync(CreateRelation(1, ownerId: "other"));

        Assert.Equal(TagCardActionResultType.Error, result.Type);
        Assert.Equal("関連付けの作成者本人ではないため、解除する権限がありません。", result.Message);
    }

    [Fact]
    public async Task RemoveRelationAsync_NotFound_ReturnsWarning()
    {
        var dataMock = new Mock<ITagTableDataProvider>();
        var lockMock = new Mock<ITagLockService>();
        dataMock.Setup(d => d.RemoveRelationAsync(1))
            .ReturnsAsync(TagCardOperationResult.NotFound);

        var vm = new TagTableViewModel(dataMock.Object, lockMock.Object);
        vm.SetUser("u1", false);

        TagCardActionResult result = await vm.RemoveRelationAsync(CreateRelation(1, ownerId: "u1"));

        Assert.Equal(TagCardActionResultType.Warning, result.Type);
        Assert.Equal("対象の関連付けが見つかりません。", result.Message);
    }

    [Fact]
    public async Task RemoveRelationAsync_Success_ReturnsSuccess()
    {
        var dataMock = new Mock<ITagTableDataProvider>();
        var lockMock = new Mock<ITagLockService>();
        dataMock.Setup(d => d.RemoveRelationAsync(1))
            .ReturnsAsync(TagCardOperationResult.Success);

        var vm = new TagTableViewModel(dataMock.Object, lockMock.Object);
        vm.SetUser("u1", false);

        TagCardActionResult result = await vm.RemoveRelationAsync(CreateRelation(1, ownerId: "u1"));

        Assert.Equal(TagCardActionResultType.Success, result.Type);
        Assert.Equal("タグの関連付けを解除しました。", result.Message);
        Assert.True(result.ShouldNotifyChanged);
    }

    [Fact]
    public async Task CheckCanEditTag_LockedAndNotAdmin_ReturnsWarning()
    {
        var dataMock = new Mock<ITagTableDataProvider>();
        var lockMock = new Mock<ITagLockService>();
        lockMock.Setup(l => l.GetAllTagsWithLockStatusAsync(default))
            .ReturnsAsync([new TagLockItemDto(1, "Tag1", 1, null, null, "u1", true, false, false, true)]);

        var vm = new TagTableViewModel(dataMock.Object, lockMock.Object);
        await vm.ReloadLockStatusAsync();
        vm.SetUser("u1", false);

        TagCardActionResult result = vm.CheckCanEditTag(CreateTag(1, ownerId: "u1"));

        Assert.Equal(TagCardActionResultType.Warning, result.Type);
    }

    [Fact]
    public void CheckCanEditTag_NotOwner_ReturnsError()
    {
        var dataMock = new Mock<ITagTableDataProvider>();
        var lockMock = new Mock<ITagLockService>();

        var vm = new TagTableViewModel(dataMock.Object, lockMock.Object);
        vm.SetUser("u1", false);

        TagCardActionResult result = vm.CheckCanEditTag(CreateTag(1, ownerId: "other"));

        Assert.Equal(TagCardActionResultType.Error, result.Type);
    }

    [Fact]
    public void CheckCanEditTag_OwnerNotLocked_ReturnsSuccess()
    {
        var dataMock = new Mock<ITagTableDataProvider>();
        var lockMock = new Mock<ITagLockService>();

        var vm = new TagTableViewModel(dataMock.Object, lockMock.Object);
        vm.SetUser("u1", false);

        TagCardActionResult result = vm.CheckCanEditTag(CreateTag(1, ownerId: "u1"));

        Assert.Equal(TagCardActionResultType.Success, result.Type);
    }

    [Fact]
    public async Task DeleteTagAsync_LockedAndNotAdmin_ReturnsWarning()
    {
        var dataMock = new Mock<ITagTableDataProvider>();
        var lockMock = new Mock<ITagLockService>();
        lockMock.Setup(l => l.GetAllTagsWithLockStatusAsync(default))
            .ReturnsAsync([new TagLockItemDto(1, "Tag1", 1, null, null, "u1", true, false, false, true)]);

        var vm = new TagTableViewModel(dataMock.Object, lockMock.Object);
        await vm.ReloadLockStatusAsync();
        vm.SetUser("u1", false);

        TagCardActionResult result = await vm.DeleteTagAsync(CreateTag(1, ownerId: "u1"));

        Assert.Equal(TagCardActionResultType.Warning, result.Type);
    }

    [Fact]
    public async Task DeleteTagAsync_SystemTag_ReturnsError()
    {
        var dataMock = new Mock<ITagTableDataProvider>();
        var lockMock = new Mock<ITagLockService>();

        var vm = new TagTableViewModel(dataMock.Object, lockMock.Object);
        vm.SetUser("u1", false);

        TagCardActionResult result = await vm.DeleteTagAsync(CreateTag(1, isSystem: true, ownerId: "u1"));

        Assert.Equal(TagCardActionResultType.Error, result.Type);
        Assert.Equal("システムタグは削除できません。", result.Message);
    }

    [Fact]
    public async Task DeleteTagAsync_NotOwner_ReturnsError()
    {
        var dataMock = new Mock<ITagTableDataProvider>();
        var lockMock = new Mock<ITagLockService>();

        var vm = new TagTableViewModel(dataMock.Object, lockMock.Object);
        vm.SetUser("u1", false);

        TagCardActionResult result = await vm.DeleteTagAsync(CreateTag(1, ownerId: "other"));

        Assert.Equal(TagCardActionResultType.Error, result.Type);
        Assert.Equal("タグの作成者本人ではないため、削除する権限がありません。", result.Message);
    }

    [Fact]
    public async Task DeleteTagAsync_Success_ReturnsSuccess()
    {
        var dataMock = new Mock<ITagTableDataProvider>();
        var lockMock = new Mock<ITagLockService>();
        dataMock.Setup(d => d.DeleteTagAsync(1, false)).ReturnsAsync(true);

        var vm = new TagTableViewModel(dataMock.Object, lockMock.Object);
        vm.SetUser("u1", false);

        TagCardActionResult result = await vm.DeleteTagAsync(CreateTag(1, ownerId: "u1"));

        Assert.Equal(TagCardActionResultType.Success, result.Type);
        Assert.Equal("タグを削除しました。", result.Message);
        Assert.True(result.ShouldNotifyChanged);
    }

    [Fact]
    public async Task DeleteTagAsync_NotFound_ReturnsWarning()
    {
        var dataMock = new Mock<ITagTableDataProvider>();
        var lockMock = new Mock<ITagLockService>();
        dataMock.Setup(d => d.DeleteTagAsync(1, false)).ReturnsAsync(false);

        var vm = new TagTableViewModel(dataMock.Object, lockMock.Object);
        vm.SetUser("u1", false);

        TagCardActionResult result = await vm.DeleteTagAsync(CreateTag(1, ownerId: "u1"));

        Assert.Equal(TagCardActionResultType.Warning, result.Type);
        Assert.Equal("対象のタグが既に削除されているか、見つかりません。", result.Message);
    }
}