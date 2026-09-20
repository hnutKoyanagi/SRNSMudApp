namespace SRNSMudApp.Tests.Components.Item;

using SRNSMudApp.Components.Item;
using SRNSMudApp.Data;
using SRNSMudApp.Models;

/// <summary>
///     AddItemViewModel の純粋な単体テスト。
///     ItemVisibility（型安全な公開範囲）、ItemPostDraft（保存データ）、
///     およびメンション抽出・通知先同期・バリデーションの振る舞いを検証する。
/// </summary>
public class AddItemViewModelTests
{
    [Fact]
    public void ItemVisibility_EnforcesScopeConstraintsAtTypeLevel()
    {
        // PublicScope: グループIDを持てない
        ItemVisibility publicScope = ItemVisibility.Public();
        Assert.False(publicScope.IsPrivate);
        Assert.Null(publicScope.TargetUserGroupId);

        // PrivateFollowersScope: フォロワー限定
        ItemVisibility followersScope = ItemVisibility.PrivateFollowers();
        Assert.True(followersScope.IsPrivate);
        Assert.Null(followersScope.TargetUserGroupId);

        // PrivateGroupScope: 特定グループ限定
        ItemVisibility groupScope = ItemVisibility.PrivateGroup(42);
        Assert.True(groupScope.IsPrivate);
        Assert.Equal(42, groupScope.TargetUserGroupId);

        // FromBooleans によるレガシー引数からの型変換
        Assert.IsType<PublicItemScope>(ItemVisibility.FromBooleans(false, 42)); // 非プライベートならGroupIdは無視
        Assert.IsType<PrivateGroupItemScope>(ItemVisibility.FromBooleans(true, 42));
        Assert.IsType<PrivateFollowersItemScope>(ItemVisibility.FromBooleans(true, null));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("新体道をやる時に哲学を", true)]
    public void CanSubmit_ValidatesContentCorrectly(string? content, bool expected)
    {
        var result = AddItemViewModel.CanSubmit(content);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CanSubmit_WhenExceedingMaxLength_ReturnsFalse()
    {
        var tooLongContent = new string('あ', AddItemViewModel.MaxContentLength + 1);
        Assert.False(AddItemViewModel.CanSubmit(tooLongContent));
        Assert.True(AddItemViewModel.IsContentTooLong(tooLongContent));

        var exactMaxContent = new string('あ', AddItemViewModel.MaxContentLength);
        Assert.True(AddItemViewModel.CanSubmit(exactMaxContent));
        Assert.False(AddItemViewModel.IsContentTooLong(exactMaxContent));
    }

    [Fact]
    public void CreateInitialItem_WithItemVisibility_SetsFieldsCorrectly()
    {
        var item = AddItemViewModel.CreateInitialItem("user-1", ItemVisibility.PrivateGroup(42));

        Assert.Equal(string.Empty, item.Content);
        Assert.Equal("user-1", item.OwnerId);
        Assert.True(item.IsPrivate);
        Assert.Equal(42, item.TargetUserGroupId);
    }

    [Fact]
    public void ExtractMentionedUserIds_ExtractsUserIdsAndExcludesCurrentUser()
    {
        var content = "新体道 /User/UserDetail/shintaido_sensei と 哲学 /User/UserDetail/tetsugaku_fan そして /User/UserDetail/me";
        var result = AddItemViewModel.ExtractMentionedUserIds(content, "me");

        Assert.Equal(2, result.Count);
        Assert.Contains("shintaido_sensei", result);
        Assert.Contains("tetsugaku_fan", result);
        Assert.DoesNotContain("me", result);
    }

    [Fact]
    public void ExtractMentionedUserIds_WithNoMentionsOrEmpty_ReturnsEmpty()
    {
        Assert.Empty(AddItemViewModel.ExtractMentionedUserIds("", "me"));
        Assert.Empty(AddItemViewModel.ExtractMentionedUserIds(null, "me"));
        Assert.Empty(AddItemViewModel.ExtractMentionedUserIds("新体道をやる時に哲学を", "me"));
    }

    [Fact]
    public void ApplyTargetToggle_UpdatesSelectedAndUnselectedSets()
    {
        HashSet<string> selected = ["shintaido_sensei"];
        HashSet<string> unselected = [];

        // メンション先のチェックを外す
        AddItemViewModel.ApplyTargetToggle("shintaido_sensei", false, selected, unselected);
        Assert.DoesNotContain("shintaido_sensei", selected);
        Assert.Contains("shintaido_sensei", unselected);

        // メンション先を再度チェックする
        AddItemViewModel.ApplyTargetToggle("shintaido_sensei", true, selected, unselected);
        Assert.Contains("shintaido_sensei", selected);
        Assert.DoesNotContain("shintaido_sensei", unselected);
    }

    [Fact]
    public void SyncSelectedTargets_WhenNotManuallyModified_SelectsAll()
    {
        List<string> candidates = ["shintaido_sensei", "tetsugaku_fan"];
        HashSet<string> selected = [];
        HashSet<string> unselected = [];

        AddItemViewModel.SyncSelectedTargets(candidates, false, selected, unselected);

        Assert.Equal(2, selected.Count);
        Assert.Contains("shintaido_sensei", selected);
        Assert.Contains("tetsugaku_fan", selected);
    }

    [Fact]
    public void SyncSelectedTargets_WhenManuallyModified_RespectsUnselectedSet()
    {
        List<string> candidates = ["shintaido_sensei", "tetsugaku_fan", "aikido_fan"];
        HashSet<string> selected = ["shintaido_sensei"];
        HashSet<string> unselected = ["tetsugaku_fan"];

        AddItemViewModel.SyncSelectedTargets(candidates, true, selected, unselected);

        Assert.Contains("shintaido_sensei", selected);
        Assert.DoesNotContain("tetsugaku_fan", selected);
        Assert.Contains("aikido_fan", selected); // 新たに追加されたメンションはデフォルト選択
    }

    [Fact]
    public void CreateDraft_And_ToItemEntity_GeneratesEntityWithRecipientsAndMergedTags()
    {
        var content = "新体道をやる時に哲学を";
        var ownerId = "author-1";
        ItemVisibility visibility = ItemVisibility.PrivateGroup(99);
        List<string> selectedRecipients = ["shintaido_sensei", "tetsugaku_fan"];
        List<int> initialTags = [10, 20]; // 10: 新体道, 20: 哲学
        List<int> suggestedTags = [20, 30]; // 20: 哲学 (重複), 30: 瞑想

        ItemPostDraft draft = AddItemViewModel.CreateDraft(
            content,
            ownerId,
            visibility,
            selectedRecipients,
            initialTags,
            suggestedTags);

        Assert.Equal(content, draft.Content);
        Assert.Equal(ownerId, draft.OwnerId);
        Assert.Equal(visibility, draft.Visibility);
        Assert.Equal([10, 20, 30], draft.TagIds);

        Item entity = draft.ToItemEntity();
        Assert.Equal(content, entity.Content);
        Assert.True(entity.IsPrivate);
        Assert.Equal(99, entity.TargetUserGroupId);
        Assert.NotNull(entity.NotificationRecipients);
        Assert.Equal(2, entity.NotificationRecipients.Count);
        Assert.Contains(entity.NotificationRecipients, r => r.RecipientUserId == "shintaido_sensei");
        Assert.Contains(entity.NotificationRecipients, r => r.RecipientUserId == "tetsugaku_fan");
    }

    [Fact]
    public void PrepareItemForSave_WhenPublic_ClearsTargetGroupIdAtTypeLevel()
    {
        (Item item, _) = AddItemViewModel.PrepareItemForSave(
            "新体道をやる時に哲学を（全体公開）",
            "author-1",
            ItemVisibility.Public(),
            selectedTargetUserIds: [],
            initialTagIds: [],
            confirmedSuggestedTagIds: []);

        Assert.False(item.IsPrivate);
        Assert.Null(item.TargetUserGroupId);
    }

    [Fact]
    public void FormatTagMentionItem_FormatsTagWithUserOrSystemCorrectly()
    {
        var userTag = new Tag
        {
            Id = 10,
            Name = "新体道",
            OwnerId = "user-1",
            Owner = new ApplicationUser { Id = "user-1", UserName = "Koyanagi" }
        };

        var systemTag = new Tag
        {
            Id = 20,
            Name = "good",
            OwnerId = "system",
            IsSystem = true
        };

        var itemUser = AddItemViewModel.FormatTagMentionItem(userTag);
        Assert.Equal("新体道 : Koyanagi", itemUser.Name);
        Assert.Equal("/TagDetail/10", itemUser.Replacement);

        var itemSystem = AddItemViewModel.FormatTagMentionItem(systemTag);
        Assert.Equal("good : system", itemSystem.Name);
        Assert.Equal("/TagDetail/20", itemSystem.Replacement);
    }

    [Fact]
    public void FormatUserMentionItem_FormatsUserCorrectly()
    {
        var user = new ApplicationUser { Id = "u-42", UserName = "Koyanagi" };
        var item = AddItemViewModel.FormatUserMentionItem(user);

        Assert.Equal("@Koyanagi", item.Name);
        Assert.Equal("/User/UserDetail/u-42", item.Replacement);
    }

    [Theory]
    [InlineData("親アイテムの本文", "返信の本文", "親アイテムの本文\n返信の本文")]
    [InlineData("  親アイテムの本文  ", "  返信の本文  ", "親アイテムの本文\n返信の本文")]
    [InlineData("親アイテムの本文", null, "親アイテムの本文")]
    [InlineData("親アイテムの本文", "", "親アイテムの本文")]
    [InlineData("親アイテムの本文", "   ", "親アイテムの本文")]
    [InlineData(null, "返信の本文", "返信の本文")]
    [InlineData("", "返信の本文", "返信の本文")]
    [InlineData("   ", "返信の本文", "返信の本文")]
    [InlineData(null, null, "")]
    [InlineData("", "", "")]
    [InlineData("   ", "   ", "")]
    public void BuildTagSuggestionQueryText_FormatsQueryCorrectly(string? parentContent, string? currentContent, string expected)
    {
        var result = AddItemViewModel.BuildTagSuggestionQueryText(currentContent, parentContent);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildTagSuggestionQueryText_WithItemEntity_ExtractsParentContent()
    {
        var parentItem = new Item { Content = "哲学と武道の関係", OwnerId = "parent-user" };
        var result = AddItemViewModel.BuildTagSuggestionQueryText("新体道における技法", parentItem);
        Assert.Equal("哲学と武道の関係\n新体道における技法", result);

        var nullParentResult = AddItemViewModel.BuildTagSuggestionQueryText("新体道における技法", (Item?)null);
        Assert.Equal("新体道における技法", nullParentResult);
    }

    [Fact]
    public void CreateDraft_And_PrepareItemForSave_PreservesParentItemIdAndRootItemId()
    {
        // Act
        var draft = AddItemViewModel.CreateDraft(
            "返信テスト",
            "user-1",
            ItemVisibility.Public(),
            ["user-2"],
            [1],
            [2],
            parentItemId: 100,
            rootItemId: 50);

        (Item itemToSave, IReadOnlyList<int> tagIds) = AddItemViewModel.PrepareItemForSave(
            "返信テスト",
            "user-1",
            ItemVisibility.Public(),
            ["user-2"],
            [1],
            [2],
            parentItemId: 100,
            rootItemId: 50);

        // Assert
        Assert.Equal(100, draft.ParentItemId);
        Assert.Equal(50, draft.RootItemId);

        Assert.Equal(100, itemToSave.ParentItemId);
        Assert.Equal(50, itemToSave.RootItemId);
        Assert.Equal("返信テスト", itemToSave.Content);
        Assert.Contains(1, tagIds);
        Assert.Contains(2, tagIds);
    }

    [Fact]
    public void CreateInitialItem_WithParentAndRootItemId_SetsPropertiesCorrectly()
    {
        var item = AddItemViewModel.CreateInitialItem("user-1", ItemVisibility.Public(), parentItemId: 77, rootItemId: 11);
        Assert.Equal(77, item.ParentItemId);
        Assert.Equal(11, item.RootItemId);

        // rootItemId 省略時は parentItemId がデフォルト
        var itemDefaultRoot = AddItemViewModel.CreateInitialItem("user-1", ItemVisibility.Public(), parentItemId: 77);
        Assert.Equal(77, itemDefaultRoot.ParentItemId);
        Assert.Equal(77, itemDefaultRoot.RootItemId);
    }
}