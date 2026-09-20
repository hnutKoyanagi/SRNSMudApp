namespace SRNSMudApp.Components.Item;

using System.Text.Json.Serialization;

using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Models;

/// <summary>
///     Tribute.js の補完候補アイテム。
/// </summary>
public record MentionItem(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("replacement")] string Replacement);

/// <summary>
///     AddItem コンポーネントに含まれる純粋なロジックを切り出した ViewModel。
///     ItemVisibility や ItemPostDraft などの型表現により、
///     公開範囲や投稿下書きのドメイン制約を型安全に管理する。
/// </summary>
public static class AddItemViewModel
{
    public const int MaxContentLength = 1000;

    /// <summary>
    ///     コンテンツが送信可能（空文字でなく、かつ文字数上限以下）かどうかを判定する。
    /// </summary>
    public static bool CanSubmit(string? content) =>
        !string.IsNullOrWhiteSpace(content) && content.Length <= MaxContentLength;

    /// <summary>
    ///     コンテンツが最大文字数（1000文字）を超えているかどうかを判定する。
    /// </summary>
    public static bool IsContentTooLong(string? content) =>
        (content?.Length ?? 0) > MaxContentLength;

    /// <summary>
    ///     初期化時のデフォルト Item オブジェクトを作成する。
    /// </summary>
    public static Item CreateInitialItem(
        string userId,
        ItemVisibility visibility,
        int? parentItemId = null,
        int? rootItemId = null) =>
        new()
        {
            Content = string.Empty,
            OwnerId = userId,
            IsPrivate = visibility.IsPrivate,
            TargetUserGroupId = visibility.TargetUserGroupId,
            ParentItemId = parentItemId,
            RootItemId = rootItemId ?? parentItemId
        };

    /// <summary>
    ///     初期化時のデフォルト Item オブジェクトを作成する（bool / int? 互換オーバーロード）。
    /// </summary>
    public static Item CreateInitialItem(
        string userId,
        bool isPrivateDefault,
        int? defaultGroupId,
        int? parentItemId = null,
        int? rootItemId = null) =>
        CreateInitialItem(userId, ItemVisibility.FromBooleans(isPrivateDefault, defaultGroupId), parentItemId, rootItemId);

    /// <summary>
    ///     テキスト中の /User/UserDetail/{userId} 形式の URL からメンション先ユーザーIDを抽出する。
    ///     ログイン中のユーザー自身は宛先から除外される。
    /// </summary>
    public static IReadOnlyList<string> ExtractMentionedUserIds(string? content, string? currentUserId)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var urls = ItemCardViewModel.ExtractUrls(content);
        var userIdsInText = urls
            .Where(u => u.StartsWith("/User/UserDetail/", StringComparison.OrdinalIgnoreCase))
            .Select(u => u["/User/UserDetail/".Length..])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (!string.IsNullOrEmpty(currentUserId))
        {
            userIdsInText.Remove(currentUserId);
        }

        return userIdsInText;
    }

    /// <summary>
    ///     手動トグル操作を反映した通知先ユーザーIDセットを計算する。
    /// </summary>
    public static void ApplyTargetToggle(
        string userId,
        bool isSelected,
        HashSet<string> selectedTargetUserIds,
        HashSet<string> unselectedTargetUserIds)
    {
        if (isSelected)
        {
            selectedTargetUserIds.Add(userId);
            unselectedTargetUserIds.Remove(userId);
        }
        else
        {
            selectedTargetUserIds.Remove(userId);
            unselectedTargetUserIds.Add(userId);
        }
    }

    /// <summary>
    ///     メンション候補リストが更新された際に、選択状態を同期する。
    /// </summary>
    public static void SyncSelectedTargets(
        IEnumerable<string> candidateUserIds,
        bool hasManuallyModified,
        HashSet<string> selectedTargetUserIds,
        IReadOnlySet<string> unselectedTargetUserIds)
    {
        foreach (var candidateId in candidateUserIds)
        {
            if (hasManuallyModified)
            {
                if (unselectedTargetUserIds.Contains(candidateId))
                {
                    selectedTargetUserIds.Remove(candidateId);
                }
                else
                {
                    selectedTargetUserIds.Add(candidateId);
                }
            }
            else
            {
                // デフォルト: 選択
                selectedTargetUserIds.Add(candidateId);
            }
        }
    }

    /// <summary>
    ///     保存用の ItemPostDraft ドメインモデルを作成する。
    /// </summary>
    public static ItemPostDraft CreateDraft(
        string content,
        string ownerId,
        ItemVisibility visibility,
        IEnumerable<string>? selectedTargetUserIds,
        IEnumerable<int>? initialTagIds,
        IEnumerable<int>? confirmedSuggestedTagIds,
        int? parentItemId = null,
        int? rootItemId = null)
    {
        var recipients = (selectedTargetUserIds ?? []).Distinct().ToList();
        var initial = initialTagIds ?? [];
        var suggested = confirmedSuggestedTagIds ?? [];
        var allTagIds = initial.Concat(suggested).Distinct().ToList();

        return new ItemPostDraft(content, ownerId, visibility, recipients, allTagIds, parentItemId, rootItemId);
    }

    /// <summary>
    ///     保存用の Item エンティティおよび付与する全タグIDリストを構築する。
    ///     ItemPostDraft を経由してドメインルールを適用する。
    /// </summary>
    public static (Item ItemToSave, IReadOnlyList<int> AllTagIds) PrepareItemForSave(
        string content,
        string ownerId,
        ItemVisibility visibility,
        IEnumerable<string>? selectedTargetUserIds,
        IEnumerable<int>? initialTagIds,
        IEnumerable<int>? confirmedSuggestedTagIds,
        int? parentItemId = null,
        int? rootItemId = null)
    {
        ItemPostDraft draft = CreateDraft(
            content, ownerId, visibility, selectedTargetUserIds, initialTagIds, confirmedSuggestedTagIds, parentItemId, rootItemId);

        return (draft.ToItemEntity(), draft.TagIds);
    }

    /// <summary>
    ///     レガシーな bool / int? 引数から ItemPostDraft を生成して保存用データを構築する互換オーバーロード。
    /// </summary>
    public static (Item ItemToSave, IReadOnlyList<int> AllTagIds) PrepareItemForSave(
        string content,
        string ownerId,
        bool isPrivate,
        int? selectedGroupId,
        IEnumerable<string>? selectedTargetUserIds,
        IEnumerable<int>? initialTagIds,
        IEnumerable<int>? confirmedSuggestedTagIds,
        int? parentItemId = null,
        int? rootItemId = null) =>
        PrepareItemForSave(
            content,
            ownerId,
            ItemVisibility.FromBooleans(isPrivate, selectedGroupId),
            selectedTargetUserIds,
            initialTagIds,
            confirmedSuggestedTagIds,
            parentItemId,
            rootItemId);

    /// <summary>
    ///     関連タグ提案の参考テキスト（クエリ文字列）を構築する。
    ///     リプライの場合は親アイテムの本文も含める。
    /// </summary>
    /// <param name="currentContent">入力中のコンテンツ本文</param>
    /// <param name="parentContent">リプライ先親アイテムの本文（リプライでない場合は null または空）</param>
    /// <returns>タグ提案のベクトル検索に使用する参照文字列</returns>
    public static string BuildTagSuggestionQueryText(string? currentContent, string? parentContent)
    {
        var hasParent = !string.IsNullOrWhiteSpace(parentContent);
        var hasCurrent = !string.IsNullOrWhiteSpace(currentContent);

        return (hasParent, hasCurrent) switch
        {
            (true, true) => $"{parentContent!.Trim()}\n{currentContent!.Trim()}",
            (true, false) => parentContent!.Trim(),
            (false, true) => currentContent!.Trim(),
            _ => string.Empty
        };
    }

    /// <summary>
    ///     関連タグ提案の参考テキスト（クエリ文字列）を構築する（Item エンティティ オーバーロード）。
    /// </summary>
    public static string BuildTagSuggestionQueryText(string? currentContent, Item? parentItem) =>
        BuildTagSuggestionQueryText(currentContent, parentItem?.Content);

    /// <summary>
    ///     タグ検索結果を Tribute.js 用の MentionItem へ整形する。
    /// </summary>
    public static MentionItem FormatTagMentionItem(Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        var ownerName = tag.Owner?.UserName
            ?? (tag.IsSystem || tag.OwnerId == "system" ? "system" : (!string.IsNullOrEmpty(tag.OwnerId) ? tag.OwnerId : "unknown"));
        return new MentionItem($"{tag.Name} : {ownerName}", $"/TagDetail/{tag.Id}");
    }

    /// <summary>
    ///     ユーザー検索結果を Tribute.js 用の MentionItem へ整形する。
    /// </summary>
    public static MentionItem FormatUserMentionItem(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new MentionItem("@" + user.UserName, $"/User/UserDetail/{user.Id}");
    }
}