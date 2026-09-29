namespace SRNSMudApp.Components.Item;

using SRNSMudApp.Data;

/// <summary>
///     ItemDetail コンポーネントのスレッド階層・折りたたみ・表示ロジックを管理する ViewModel。
///     Blazor のコンポーネントレンダリングから独立して単体テスト可能。
/// </summary>
public sealed class ItemDetailThreadViewModel
{
    public const int AncestorsThreshold = 4;
    public const int EarlierSiblingsThreshold = 4;
    public const int LaterSiblingsThreshold = 2;
    public const int RepliesThreshold = 3;

    public bool IsAncestorsExpanded { get; private set; }
    public bool IsEarlierSiblingsExpanded { get; private set; }
    public bool IsLaterSiblingsExpanded { get; private set; }
    public bool IsRepliesExpanded { get; private set; }

    /// <summary>
    ///     全ての折りたたみ状態を展開前にリセットする。
    /// </summary>
    public void ResetExpansion()
    {
        IsAncestorsExpanded = false;
        IsEarlierSiblingsExpanded = false;
        IsLaterSiblingsExpanded = false;
        IsRepliesExpanded = false;
    }

    public void ExpandAncestors() => IsAncestorsExpanded = true;
    public void ExpandEarlierSiblings() => IsEarlierSiblingsExpanded = true;
    public void ExpandLaterSiblings() => IsLaterSiblingsExpanded = true;
    public void ExpandReplies() => IsRepliesExpanded = true;

    /// <summary>
    ///     先祖アイテムの折りたたみを行うべきかどうかを判定する。
    /// </summary>
    public bool ShouldCollapseAncestors(int totalCount) =>
        !IsAncestorsExpanded && totalCount > AncestorsThreshold;

    /// <summary>
    ///     折りたたまれている先祖アイテムの件数を取得する（最初と最後を除く件数）。
    /// </summary>
    public static int GetHiddenAncestorsCount(int totalCount) =>
        totalCount > AncestorsThreshold ? totalCount - 2 : 0;

    /// <summary>
    ///     兄弟アイテムを現在選択中のアイテムを基準に、過去（作成日時が前）と未来（作成日時が後）に分割する。
    /// </summary>
    public static (IReadOnlyList<Item> Earlier, IReadOnlyList<Item> Later) SplitSiblings(
        Item? currentItem,
        IEnumerable<Item>? siblings)
    {
        if (currentItem is null || siblings is null)
        {
            return ([], []);
        }

        var siblingReplies = siblings.Concat([currentItem]).OrderBy(i => i.CreatedDate).ToList();
        var itemIndex = siblingReplies.FindIndex(i => i.Id == currentItem.Id);
        if (itemIndex < 0)
        {
            return ([], []);
        }

        var earlierSiblings = itemIndex > 0 ? siblingReplies.Take(itemIndex).ToList() : [];
        var laterSiblings = itemIndex < siblingReplies.Count - 1
            ? siblingReplies.Skip(itemIndex + 1).ToList()
            : [];

        return (earlierSiblings, laterSiblings);
    }

    /// <summary>
    ///     過去の兄弟リプライの折りたたみを行うべきかを判定する。
    /// </summary>
    public bool ShouldCollapseEarlierSiblings(int totalCount) =>
        !IsEarlierSiblingsExpanded && totalCount > EarlierSiblingsThreshold;

    /// <summary>
    ///     折りたたまれている過去兄弟リプライの件数を取得する。
    /// </summary>
    public static int GetHiddenEarlierSiblingsCount(int totalCount) =>
        totalCount > EarlierSiblingsThreshold ? totalCount - EarlierSiblingsThreshold : 0;

    /// <summary>
    ///     表示対象となる過去の兄弟リプライ一覧を取得する。
    /// </summary>
    public IReadOnlyList<Item> GetVisibleEarlierSiblings(IReadOnlyList<Item> earlierSiblings)
    {
        return ShouldCollapseEarlierSiblings(earlierSiblings.Count)
            ? earlierSiblings.TakeLast(EarlierSiblingsThreshold).ToList()
            : earlierSiblings;
    }

    /// <summary>
    ///     新しい兄弟リプライの折りたたみを行うべきかを判定する。
    /// </summary>
    public bool ShouldCollapseLaterSiblings(int totalCount) =>
        !IsLaterSiblingsExpanded && totalCount > LaterSiblingsThreshold;

    /// <summary>
    ///     折りたたまれている新しい兄弟リプライの件数を取得する。
    /// </summary>
    public static int GetHiddenLaterSiblingsCount(int totalCount) =>
        totalCount > LaterSiblingsThreshold ? totalCount - LaterSiblingsThreshold : 0;

    /// <summary>
    ///     表示対象となる新しい兄弟リプライ一覧を取得する。
    /// </summary>
    public IReadOnlyList<Item> GetVisibleLaterSiblings(IReadOnlyList<Item> laterSiblings)
    {
        return ShouldCollapseLaterSiblings(laterSiblings.Count)
            ? laterSiblings.Take(LaterSiblingsThreshold).ToList()
            : laterSiblings;
    }

    /// <summary>
    ///     リプライ一覧の折りたたみを行うべきかを判定する。
    /// </summary>
    public bool ShouldCollapseReplies(int totalCount) =>
        !IsRepliesExpanded && totalCount > RepliesThreshold;

    /// <summary>
    ///     折りたたまれているリプライの件数を取得する。
    /// </summary>
    public static int GetHiddenRepliesCount(int totalCount) =>
        totalCount > RepliesThreshold ? totalCount - RepliesThreshold : 0;

    /// <summary>
    ///     表示対象となるリプライ一覧を取得する。
    /// </summary>
    public IReadOnlyList<Item> GetVisibleReplies(IReadOnlyList<Item> replies)
    {
        return ShouldCollapseReplies(replies.Count)
            ? replies.Take(RepliesThreshold).ToList()
            : replies;
    }

    /// <summary>
    ///     リプライ時のプライベートフラグおよび所属グループIDを解決する。
    /// </summary>
    public static (bool IsPrivate, int? TargetGroupId) ResolveReplyPrivacy(
        bool isReplyPrivate,
        Item? parentItem)
    {
        int? targetGroupId = isReplyPrivate ? parentItem?.TargetUserGroupId : null;
        return (isReplyPrivate, targetGroupId);
    }
}