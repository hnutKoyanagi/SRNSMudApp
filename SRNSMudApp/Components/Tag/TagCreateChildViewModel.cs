namespace SRNSMudApp.Components.Tag;

/// <summary>
///     子タグ新規作成結果レコード。
/// </summary>
public sealed record TagCreateChildResult(string Name, string Content);

/// <summary>
///     子タグ作成ダイアログ (TagCreateChildDialog) の入力状態およびバリデーションを担う ViewModel。
/// </summary>
public sealed class TagCreateChildViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    /// <summary>
    ///     タグ名が入力されているか判定する。
    /// </summary>
    public bool CanSubmit => !string.IsNullOrWhiteSpace(Name);

    /// <summary>
    ///     確定結果の DTO を生成する（無効な場合は null）。
    /// </summary>
    public TagCreateChildResult? CreateResult()
    {
        if (!CanSubmit)
        {
            return null;
        }

        return new TagCreateChildResult(Name.Trim(), Content?.Trim() ?? string.Empty);
    }
}