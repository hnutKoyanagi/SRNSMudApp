namespace SRNSMudApp.Models;

/// <summary>
///     タグチップの背景色およびテキスト色の計算ロジック・カラー定数を一元管理する計算クラス。
/// </summary>
public static class ChipColorCalculator
{
    /// <summary>削除イベント時の背景色。</summary>
    public const string DeletedBackgroundColor = "#E0E0E0";

    /// <summary>削除イベント時のテキスト色。</summary>
    public const string DeletedTextColor = "#9E9E9E";

    /// <summary>ハイライト（追加・更新・投票等）時の背景色。</summary>
    public const string HighlightBackgroundColor = "#FFEB3B";

    /// <summary>ハイライト時のテキスト色。</summary>
    public const string HighlightTextColor = "#F57F17";

    /// <summary>通常（他ユーザー作成）タグチップの背景色。</summary>
    public const string DefaultBackgroundColor = "#FFF9C4";

    /// <summary>通常（他ユーザー作成）タグチップのテキスト色。</summary>
    public const string DefaultTextColor = "#5C4B00";

    /// <summary>自分のタグチップのフォールバック背景色。</summary>
    public const string DefaultMyTagBackgroundColor = "#EEEDFE";

    /// <summary>自分のタグチップのフォールバックテキスト色。</summary>
    public const string DefaultMyTagTextColor = "#26215C";

    /// <summary>
    ///     チップの状態（削除・ハイライト・所有者）に応じた背景色を算出する。
    /// </summary>
    /// <param name="isDeleted">削除イベント対象かどうか。</param>
    /// <param name="hasHighlight">ハイライト対象イベントがあるかどうか。</param>
    /// <param name="isMyTag">閲覧ユーザー自身のタグかどうか。</param>
    /// <param name="chipIndex">インデックス（複数カラーパレット適用用）。</param>
    /// <param name="backgrounds">自分用チップ背景色パレット。未指定または空の場合はデフォルト色を使用。</param>
    /// <returns>16進カラーコード文字列。</returns>
    public static string GetBackgroundColor(
        bool isDeleted,
        bool hasHighlight,
        bool isMyTag,
        int chipIndex,
        IReadOnlyList<string>? backgrounds) =>
        isDeleted switch
        {
            true => DeletedBackgroundColor,
            false => hasHighlight switch
            {
                true => HighlightBackgroundColor,
                false => isMyTag switch
                {
                    true => (backgrounds != null && backgrounds.Count > 0)
                        ? (chipIndex >= 0 && chipIndex < backgrounds.Count ? backgrounds[chipIndex] : backgrounds[0])
                        : DefaultMyTagBackgroundColor,
                    false => DefaultBackgroundColor
                }
            }
        };

    /// <summary>
    ///     チップの状態（削除・ハイライト・所有者）に応じたテキスト色を算出する。
    /// </summary>
    /// <param name="isDeleted">削除イベント対象かどうか。</param>
    /// <param name="hasHighlight">ハイライト対象イベントがあるかどうか。</param>
    /// <param name="isMyTag">閲覧ユーザー自身のタグかどうか。</param>
    /// <param name="chipIndex">インデックス（複数カラーパレット適用用）。</param>
    /// <param name="textColors">自分用チップテキスト色パレット。未指定または空の場合はデフォルト色を使用。</param>
    /// <returns>16進カラーコード文字列。</returns>
    public static string GetTextColor(
        bool isDeleted,
        bool hasHighlight,
        bool isMyTag,
        int chipIndex,
        IReadOnlyList<string>? textColors) =>
        isDeleted switch
        {
            true => DeletedTextColor,
            false => hasHighlight switch
            {
                true => HighlightTextColor,
                false => isMyTag switch
                {
                    true => (textColors != null && textColors.Count > 0)
                        ? (chipIndex >= 0 && chipIndex < textColors.Count ? textColors[chipIndex] : textColors[0])
                        : DefaultMyTagTextColor,
                    false => DefaultTextColor
                }
            }
        };
}