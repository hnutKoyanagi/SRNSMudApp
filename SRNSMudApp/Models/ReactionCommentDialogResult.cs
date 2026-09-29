namespace SRNSMudApp.Models;

/// <summary>
///     リアクションコメント入力ダイアログの結果。
/// </summary>
/// <param name="Saved">リアクションを適用（保存）する場合は true、キャンセルの場合は false。</param>
/// <param name="Comment">入力されたコメント文字列（未入力またはタイムアウト時は null）。</param>
public sealed record ReactionCommentDialogResult(bool Saved, string? Comment);