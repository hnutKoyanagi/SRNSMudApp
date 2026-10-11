namespace SRNSMudApp.Models;

/// <summary>
///     UI表示（チップ、カード、アラート等）に関する共通表示整形ヘルパー。
/// </summary>
public static class DisplayHelpers
{
    /// <summary>オーナー名の最大表示文字数。</summary>
    public const int MaxOwnerNameLength = 7;

    /// <summary>オーナー名が null または空の場合の代替表示文字列。</summary>
    public const string UnknownOwnerName = "不明";

    /// <summary>
    ///     オーナー名を最大7文字に短縮して返す。null/空の場合は「不明」を返す。
    /// </summary>
    /// <param name="name">元のユーザー名。</param>
    /// <returns>短縮後のユーザー名、または「不明」。</returns>
    public static string GetShortOwnerName(string? name) =>
        string.IsNullOrEmpty(name) switch
        {
            true => UnknownOwnerName,
            false => name.Length > MaxOwnerNameLength ? name[..MaxOwnerNameLength] : name
        };
}