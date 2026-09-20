namespace SRNSMudApp.Models;

/// <summary>
///     本番・開発環境の初期化用システムタグシードデータモデル。
/// </summary>
public class SystemTagSeedModel
{
    /// <summary>タグ名。</summary>
    public required string Name { get; set; }

    /// <summary>タグの説明・内容。</summary>
    public string Content { get; set; } = "";

    /// <summary>HierarchyId 文字列表現（例: "/7/", "/7/1/"）。</summary>
    public required string Node { get; set; }

    /// <summary>親ノードの HierarchyId 文字列表現（例: "/", "/7/"）。</summary>
    public required string ParentNode { get; set; }

    /// <summary>タグがロックされているかどうか。</summary>
    public bool IsLocked { get; set; }

    /// <summary>タグ付けリクエストの自動承認設定。</summary>
    public bool AutoAcceptIncomingTaggingRequests { get; set; }
}

