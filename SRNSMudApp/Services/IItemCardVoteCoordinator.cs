using SRNSMudApp.Data;

namespace SRNSMudApp.Services;

/// <summary>
///     ItemCard における投票（Good）および三相リアクション（真実・善・美）の操作を調整するコーディネーター。
/// </summary>
public interface IItemCardVoteCoordinator
{
    /// <summary>
    ///     アイテムへの Good 投票（アップボート / ダウンボート）を切り替える。
    /// </summary>
    /// <param name="itemId">対象アイテムの ID。</param>
    /// <param name="currentUserId">現在ログインしているユーザーの ID。</param>
    /// <param name="goodTagId">Good 判定に使用するタグの ID（未解決の場合は null）。</param>
    /// <param name="isUpvote">アップボート（+1）の場合は true、ダウンボート（-1）の場合は false。</param>
    /// <param name="ensureSystemTagsAsync">システムタグの初期化が必要な場合のコールバック（省略可能）。</param>
    /// <returns>投票操作が正常に完了した場合は true、未ログインやタグ解決失敗などにより中止された場合は false。</returns>
    Task<bool> ToggleVoteAsync(
        int itemId,
        string currentUserId,
        int? goodTagId,
        bool isUpvote,
        Func<Task>? ensureSystemTagsAsync = null);

    /// <summary>
    ///     アイテムへの三相リアクション投票を切り替える。
    /// </summary>
    /// <param name="itemId">対象アイテムの ID。</param>
    /// <param name="currentUserId">現在ログインしているユーザーの ID。</param>
    /// <param name="reactionTagName">リアクションタグの名称（真実、善、美）。</param>
    /// <param name="targetWeight">投票の重み（通常は 1）。</param>
    /// <param name="reactionTagId">既に解決済みのリアクションタグ ID（未指定の場合は null）。</param>
    /// <param name="allTags">キャッシュまたは取得済みのタグ一覧。</param>
    /// <param name="ensureSystemTagsAsync">システムタグの初期化が必要な場合のコールバック（省略可能）。</param>
    /// <param name="comment">タグ関連付けに付与するコメント（省略可能）。</param>
    /// <returns>リアクション操作の結果（未ログイン等により中止された場合は null）。</returns>
    Task<ItemVoteResult?> ToggleReactionAsync(
        int itemId,
        string currentUserId,
        string reactionTagName,
        int targetWeight,
        int? reactionTagId,
        IReadOnlyList<Tag> allTags,
        Func<Task>? ensureSystemTagsAsync = null,
        string? comment = null);

    /// <summary>
    ///     リアクション（TagRelation）のコメント（CommentItem）を更新する。
    /// </summary>
    /// <param name="relationId">対象の TagRelation ID。</param>
    /// <param name="currentUserId">現在ログインしているユーザーの ID。</param>
    /// <param name="comment">更新するコメント文字列。</param>
    /// <returns>更新に成功した場合は true、対象が見つからないか未ログイン等の場合は false。</returns>
    Task<bool> UpdateReactionCommentAsync(
        int relationId,
        string currentUserId,
        string? comment);
}