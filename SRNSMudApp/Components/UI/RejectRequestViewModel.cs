namespace SRNSMudApp.Components.UI;

/// <summary>
///     リクエスト却下理由入力ダイアログ (RejectRequestDialog) の状態管理を担う ViewModel。
/// </summary>
public sealed class RejectRequestViewModel
{
    public RejectRequestViewModel()
    {
    }

    public RejectRequestViewModel(string? initialComment)
    {
        Comment = initialComment;
    }

    public string? Comment { get; set; }

    /// <summary>
    ///     正規化されたコメント文字列（トリム済み、null は空文字列）。
    /// </summary>
    public string NormalizedComment => Comment?.Trim() ?? string.Empty;

    /// <summary>
    ///     確定結果のコメント文字列。
    /// </summary>
    public string SubmitResult => NormalizedComment;
}