namespace SRNSMudApp.Tests.Components.Item;

using SRNSMudApp.Components.Item;
using SRNSMudApp.Data;

/// <summary>
///     ItemDetailThreadViewModel の単体テスト。
///     bUnit や Blazor のレンダリングツリーを介さずに、スレッド表示・折りたたみ・プライベート設定ロジックを高速に検証する。
/// </summary>
public class ItemDetailThreadViewModelTests
{
    // ────────────────────────────────────────────────────────────
    // SplitSiblings
    // ────────────────────────────────────────────────────────────

    private static Item CreateItem(int id, DateTime? created = null, bool isPrivate = false, int? targetUserGroupId = null) => new()
    {
        Id = id,
        OwnerId = "test-user",
        CreatedDate = created ?? DateTime.UtcNow,
        IsPrivate = isPrivate,
        TargetUserGroupId = targetUserGroupId
    };

    [Fact]
    public void SplitSiblings_SplitsIntoEarlierAndLaterChronologically()
    {
        var currentItem = CreateItem(10, new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc));
        var earlier1 = CreateItem(1, new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));
        var earlier2 = CreateItem(2, new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc));
        var later1 = CreateItem(20, new DateTime(2026, 9, 1, 13, 0, 0, DateTimeKind.Utc));

        // 順不同で渡す
        List<Item> siblings = [later1, earlier2, earlier1];

        (IReadOnlyList<Item> earlier, IReadOnlyList<Item> later) =
            ItemDetailThreadViewModel.SplitSiblings(currentItem, siblings);

        Assert.Equal(2, earlier.Count);
        Assert.Equal(1, earlier[0].Id);
        Assert.Equal(2, earlier[1].Id);

        Assert.Single(later);
        Assert.Equal(20, later[0].Id);
    }

    [Fact]
    public void SplitSiblings_WhenSiblingsIsNull_ReturnsEmptyLists()
    {
        var currentItem = CreateItem(1);
        (IReadOnlyList<Item> earlier, IReadOnlyList<Item> later) =
            ItemDetailThreadViewModel.SplitSiblings(currentItem, null);

        Assert.Empty(earlier);
        Assert.Empty(later);
    }

    // ────────────────────────────────────────────────────────────
    // Ancestors Folding
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Ancestors_WhenFourOrLess_DoesNotCollapse()
    {
        var vm = new ItemDetailThreadViewModel();

        Assert.False(vm.ShouldCollapseAncestors(4));
        Assert.Equal(0, ItemDetailThreadViewModel.GetHiddenAncestorsCount(4));
    }

    [Fact]
    public void Ancestors_WhenGreaterThanFour_CollapsesUntilExpanded()
    {
        var vm = new ItemDetailThreadViewModel();

        // 5件の場合: 閾値超えで折りたたみ対象、最古と直前を除く 3件が隠れる
        Assert.True(vm.ShouldCollapseAncestors(5));
        Assert.Equal(3, ItemDetailThreadViewModel.GetHiddenAncestorsCount(5));

        // 展開すると折りたたまれなくなる
        vm.ExpandAncestors();
        Assert.True(vm.IsAncestorsExpanded);
        Assert.False(vm.ShouldCollapseAncestors(5));
    }

    // ────────────────────────────────────────────────────────────
    // EarlierSiblings Folding
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void EarlierSiblings_WhenFourOrLess_DoesNotCollapse()
    {
        var vm = new ItemDetailThreadViewModel();

        List<Item> items = Enumerable.Range(1, 4).Select(i => CreateItem(i)).ToList();

        Assert.False(vm.ShouldCollapseEarlierSiblings(items.Count));
        Assert.Equal(0, ItemDetailThreadViewModel.GetHiddenEarlierSiblingsCount(items.Count));
        Assert.Equal(4, vm.GetVisibleEarlierSiblings(items).Count);
    }

    [Fact]
    public void EarlierSiblings_WhenGreaterThanFour_CollapsesToLatestFour()
    {
        var vm = new ItemDetailThreadViewModel();

        // 5件の場合 (ID: 1..5)
        List<Item> items = Enumerable.Range(1, 5).Select(i => CreateItem(i)).ToList();

        Assert.True(vm.ShouldCollapseEarlierSiblings(items.Count));
        Assert.Equal(1, ItemDetailThreadViewModel.GetHiddenEarlierSiblingsCount(items.Count));

        // 直近4件 (2, 3, 4, 5) のみが可視
        IReadOnlyList<Item> visible = vm.GetVisibleEarlierSiblings(items);
        Assert.Equal(4, visible.Count);
        Assert.Equal(2, visible[0].Id);
        Assert.Equal(5, visible[^1].Id);

        // 展開後
        vm.ExpandEarlierSiblings();
        Assert.True(vm.IsEarlierSiblingsExpanded);
        Assert.False(vm.ShouldCollapseEarlierSiblings(items.Count));
        Assert.Equal(5, vm.GetVisibleEarlierSiblings(items).Count);
    }

    // ────────────────────────────────────────────────────────────
    // LaterSiblings Folding
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void LaterSiblings_WhenTwoOrLess_DoesNotCollapse()
    {
        var vm = new ItemDetailThreadViewModel();

        List<Item> items = Enumerable.Range(1, 2).Select(i => CreateItem(i)).ToList();

        Assert.False(vm.ShouldCollapseLaterSiblings(items.Count));
        Assert.Equal(0, ItemDetailThreadViewModel.GetHiddenLaterSiblingsCount(items.Count));
        Assert.Equal(2, vm.GetVisibleLaterSiblings(items).Count);
    }

    [Fact]
    public void LaterSiblings_WhenGreaterThanTwo_CollapsesToFirstTwo()
    {
        var vm = new ItemDetailThreadViewModel();

        // 3件の場合 (ID: 1..3)
        List<Item> items = Enumerable.Range(1, 3).Select(i => CreateItem(i)).ToList();

        Assert.True(vm.ShouldCollapseLaterSiblings(items.Count));
        Assert.Equal(1, ItemDetailThreadViewModel.GetHiddenLaterSiblingsCount(items.Count));

        // 直近2件 (1, 2) のみが可視
        IReadOnlyList<Item> visible = vm.GetVisibleLaterSiblings(items);
        Assert.Equal(2, visible.Count);
        Assert.Equal(1, visible[0].Id);
        Assert.Equal(2, visible[1].Id);

        // 展開後
        vm.ExpandLaterSiblings();
        Assert.True(vm.IsLaterSiblingsExpanded);
        Assert.False(vm.ShouldCollapseLaterSiblings(items.Count));
        Assert.Equal(3, vm.GetVisibleLaterSiblings(items).Count);
    }

    // ────────────────────────────────────────────────────────────
    // Replies Folding
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Replies_WhenThreeOrLess_DoesNotCollapse()
    {
        var vm = new ItemDetailThreadViewModel();

        List<Item> items = Enumerable.Range(1, 3).Select(i => CreateItem(i)).ToList();

        Assert.False(vm.ShouldCollapseReplies(items.Count));
        Assert.Equal(0, ItemDetailThreadViewModel.GetHiddenRepliesCount(items.Count));
        Assert.Equal(3, vm.GetVisibleReplies(items).Count);
    }

    [Fact]
    public void Replies_WhenGreaterThanThree_CollapsesToFirstThree()
    {
        var vm = new ItemDetailThreadViewModel();

        // 4件の場合 (ID: 1..4)
        List<Item> items = Enumerable.Range(1, 4).Select(i => CreateItem(i)).ToList();

        Assert.True(vm.ShouldCollapseReplies(items.Count));
        Assert.Equal(1, ItemDetailThreadViewModel.GetHiddenRepliesCount(items.Count));

        // 最初の3件 (1, 2, 3) のみが可視
        IReadOnlyList<Item> visible = vm.GetVisibleReplies(items);
        Assert.Equal(3, visible.Count);
        Assert.Equal(1, visible[0].Id);
        Assert.Equal(3, visible[2].Id);

        // 展開後
        vm.ExpandReplies();
        Assert.True(vm.IsRepliesExpanded);
        Assert.False(vm.ShouldCollapseReplies(items.Count));
        Assert.Equal(4, vm.GetVisibleReplies(items).Count);
    }

    // ────────────────────────────────────────────────────────────
    // ResetExpansion
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void ResetExpansion_ResetsAllExpandedFlags()
    {
        var vm = new ItemDetailThreadViewModel();
        vm.ExpandAncestors();
        vm.ExpandEarlierSiblings();
        vm.ExpandLaterSiblings();
        vm.ExpandReplies();

        vm.ResetExpansion();

        Assert.False(vm.IsAncestorsExpanded);
        Assert.False(vm.IsEarlierSiblingsExpanded);
        Assert.False(vm.IsLaterSiblingsExpanded);
        Assert.False(vm.IsRepliesExpanded);
    }

    // ────────────────────────────────────────────────────────────
    // ResolveReplyPrivacy
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void ResolveReplyPrivacy_WhenPrivateWithGroup_ReturnsPrivateAndGroupId()
    {
        var parent = CreateItem(1, isPrivate: true, targetUserGroupId: 42);

        (bool isPrivate, int? groupId) = ItemDetailThreadViewModel.ResolveReplyPrivacy(true, parent);

        Assert.True(isPrivate);
        Assert.Equal(42, groupId);
    }

    [Fact]
    public void ResolveReplyPrivacy_WhenNotPrivate_ReturnsFalseAndNull()
    {
        var parent = CreateItem(1, isPrivate: true, targetUserGroupId: 42);

        (bool isPrivate, int? groupId) = ItemDetailThreadViewModel.ResolveReplyPrivacy(false, parent);

        Assert.False(isPrivate);
        Assert.Null(groupId);
    }

    [Fact]
    public void ResolveReplyPrivacy_WhenParentIsNull_ReturnsPrivateAndNull()
    {
        (bool isPrivate, int? groupId) = ItemDetailThreadViewModel.ResolveReplyPrivacy(true, null);

        Assert.True(isPrivate);
        Assert.Null(groupId);
    }
}