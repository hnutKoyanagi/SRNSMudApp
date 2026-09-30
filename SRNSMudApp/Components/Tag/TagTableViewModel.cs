using System.Diagnostics.CodeAnalysis;

using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Components.Tag;

/// <summary>
///     添付タグ列の表示状態。
/// </summary>
public readonly record struct AttachedTagsDisplay(
    IReadOnlyList<TagRelationToTag> TagsToDisplay,
    int HiddenCount,
    bool HasManyTags,
    bool IsExpanded)
{
    public const int DisplayLimit = 2;
    public const int ManyTagsThreshold = 3;

    /// <summary>「閉じる」または「+N more」のラベル文字列を返す。</summary>
    public string ToggleLabel => IsExpanded ? "閉じる" : $"+{HiddenCount} more";
}

/// <summary>
///     TagTable コンポーネントに含まれる表示ロジックおよびデータ操作ロジックを集約する ViewModel。
///     UI への依存を持たないため、bUnit を使わずに xUnit で直接単体テストできる。
/// </summary>
public class TagTableViewModel
{
    private readonly ITagTableDataProvider _tagTableData;
    private readonly ITagLockService _tagLockService;
    private HashSet<int> _lockedTagIds = [];

    public TagTableViewModel(ITagTableDataProvider tagTableData, ITagLockService tagLockService)
    {
        _tagTableData = tagTableData;
        _tagLockService = tagLockService;
    }

    [SuppressMessage("Usage", "CA1002:Do not expose generic lists", Justification = "Blazor MudTable binding requirement")]
    public List<Data.Tag> AllTagsCache { get; private set; } = [];
    public IReadOnlySet<int> LockedTagIds => _lockedTagIds;
    public string CurrentUserId { get; private set; } = "";
    public bool IsAdmin { get; private set; }

    public void SetUser(string currentUserId, bool isAdmin)
    {
        CurrentUserId = currentUserId;
        IsAdmin = isAdmin;
    }

    public async Task InitializeAsync()
    {
        AllTagsCache = await _tagTableData.GetAllTagsAsync();
        await ReloadLockStatusAsync();
    }

    public async Task ReloadLockStatusAsync()
    {
        var allStatus = await _tagLockService.GetAllTagsWithLockStatusAsync();
        _lockedTagIds = allStatus.Where(s => s.IsLockedEffective).Select(s => s.Id).ToHashSet();
    }

    public bool IsTagLocked(int tagId) => _lockedTagIds.Contains(tagId);

    public async Task<TagCardActionResult> AddRelationAsync(int targetTagId, int selectedTagId)
    {
        TagCardOperationResult result =
            await _tagTableData.AddRelationAsync(targetTagId, selectedTagId, CurrentUserId);

        return result switch
        {
            TagCardOperationResult.AlreadyExists =>
                TagCardActionResult.Warning("このタグは既に追加されています。"),
            TagCardOperationResult.Success =>
                TagCardActionResult.Success("タグを追加しました。"),
            TagCardOperationResult.NotFound =>
                TagCardActionResult.Warning("対象のタグが見つかりません。"),
            TagCardOperationResult.NotOwner =>
                TagCardActionResult.Error("権限がありません。"),
            _ => TagCardActionResult.NoOp()
        };
    }

    public async Task<TagCardActionResult> RemoveRelationAsync(TagRelationToTag relation)
    {
        if (!CanRemoveRelation(relation, CurrentUserId))
        {
            return TagCardActionResult.Error("関連付けの作成者本人ではないため、解除する権限がありません。");
        }

        TagCardOperationResult result = await _tagTableData.RemoveRelationAsync(relation.Id);
        return result switch
        {
            TagCardOperationResult.Success =>
                TagCardActionResult.Success("タグの関連付けを解除しました。"),
            TagCardOperationResult.NotFound =>
                TagCardActionResult.Warning("対象の関連付けが見つかりません。"),
            TagCardOperationResult.AlreadyExists =>
                TagCardActionResult.NoOp(),
            TagCardOperationResult.NotOwner =>
                TagCardActionResult.Error("権限がありません。"),
            _ => TagCardActionResult.NoOp()
        };
    }

    public TagCardActionResult CheckCanEditTag(Data.Tag tag)
    {
        if (CanEditTag(tag, CurrentUserId, IsTagLocked(tag.Id), IsAdmin))
        {
            return TagCardActionResult.Success(shouldNotifyChanged: false);
        }

        if (IsTagLocked(tag.Id) && !IsAdmin)
        {
            return TagCardActionResult.Warning("このタグまたはその兄弟タグはロックされているため編集できません。");
        }

        return TagCardActionResult.Error("タグの作成者本人ではないため、編集する権限がありません。");
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "データ削除時の予期しない例外をUI向けメッセージに変換するため")]
    public async Task<TagCardActionResult> DeleteTagAsync(Data.Tag tag)
    {
        if (IsTagLocked(tag.Id) && !IsAdmin)
        {
            return TagCardActionResult.Warning("このタグまたはその兄弟タグはロックされているため削除できません。");
        }

        if (!CanDeleteTag(tag, CurrentUserId, IsTagLocked(tag.Id), IsAdmin))
        {
            return tag.IsSystem
                ? TagCardActionResult.Error("システムタグは削除できません。")
                : TagCardActionResult.Error("タグの作成者本人ではないため、削除する権限がありません。");
        }

        try
        {
            var deleted = await _tagTableData.DeleteTagAsync(tag.Id, IsAdmin);
            return deleted
                ? TagCardActionResult.Success("タグを削除しました。")
                : TagCardActionResult.Warning("対象のタグが既に削除されているか、見つかりません。");
        }
        catch (Exception ex)
        {
            return TagCardActionResult.Error($"エラーが発生しました: {ex.Message}");
        }
    }

    /// <summary>
    ///     MudTable のフィルタ条件。検索語が空の場合はすべて表示する。
    /// </summary>
    public static bool FilterFunc(Data.Tag tag, string? search)
    {
        return string.IsNullOrWhiteSpace(search) ||
               tag.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               tag.Content?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
               tag.Owner?.UserName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    ///     オートコンプリート用のタグ名候補を返す。重複は除去し最大 20 件に制限する。
    /// </summary>
    public static IReadOnlyList<string> GetTagSearchSuggestions(IEnumerable<Data.Tag>? sourceTags, string? value)
    {
        IEnumerable<Data.Tag> tags = sourceTags ?? [];
        return string.IsNullOrEmpty(value)
            ? [.. tags.Select(t => t.Name).Distinct().Take(20)]
            : [.. tags
                .Where(t => t.Name.Contains(value, StringComparison.OrdinalIgnoreCase))
                .Select(t => t.Name)
                .Distinct()
                .Take(20)];
    }

    /// <summary>
    ///     対象タグに添付された（システムタグ以外の）タグ関係をウェイト降順で返す。
    /// </summary>
    public static IReadOnlyList<TagRelationToTag> GetAttachedTags(Data.Tag tag)
    {
        return tag.TargetTagRelations?
                   .Where(tr => tr.Tag?.IsSystem == false)
                   .OrderByDescending(tr => tr.Weight)
                   .ToList()
               ?? [];
    }

    /// <summary>
    ///     展開状態を考慮して、添付タグ列に表示するタグ関係と隠し件数を計算する。
    /// </summary>
    public static AttachedTagsDisplay GetAttachedTagsDisplay(Data.Tag tag, bool isExpanded)
    {
        IReadOnlyList<TagRelationToTag> allTags = GetAttachedTags(tag);
        var hasManyTags = allTags.Count >= AttachedTagsDisplay.ManyTagsThreshold;
        var hiddenCount = hasManyTags && !isExpanded
            ? allTags.Count - AttachedTagsDisplay.DisplayLimit
            : 0;

        return new AttachedTagsDisplay(
            hasManyTags && !isExpanded
                ? [.. allTags.Take(AttachedTagsDisplay.DisplayLimit)]
                : allTags,
            hiddenCount,
            hasManyTags,
            isExpanded);
    }

    /// <summary>
    ///     現在のユーザーがそのタグを編集できるかどうかを返す（ロックされている場合は不可。ただし Admin は操作可能）。
    /// </summary>
    public static bool CanEditTag(Data.Tag tag, string? currentUserId, bool isLocked = false, bool isAdmin = false) =>
        isAdmin || (!isLocked && tag.OwnerId == currentUserId);

    /// <summary>
    ///     現在のユーザーがそのタグを削除できるかどうかを返す（システムタグは不可。ロックされているタグは Admin のみ可能）。
    /// </summary>
    public static bool CanDeleteTag(Data.Tag tag, string? currentUserId, bool isLocked = false, bool isAdmin = false) =>
        !tag.IsSystem && (isAdmin || (!isLocked && tag.OwnerId == currentUserId));

    /// <summary>
    ///     現在のユーザーがそのタグ関連付けを解除できるかどうかを返す。
    /// </summary>
    public static bool CanRemoveRelation(TagRelationToTag relation, string? currentUserId) =>
        relation.OwnerId == currentUserId;
}