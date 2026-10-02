using SRNSMudApp.Data;

namespace SRNSMudApp.Components.Diagram;

/// <summary>
///     エッジ詳細パネル (TagEdgeInspector) のツリー展開状態および権限判定ロジックを管理する ViewModel。
/// </summary>
public sealed class TagEdgeInspectorViewModel
{
    /// <summary>現在選択されているエッジ。</summary>
    public TagEdge? Edge { get; set; }

    /// <summary>ログイン中のユーザー ID。</summary>
    public string CurrentUserId { get; set; } = string.Empty;

    /// <summary>現在タグツリーを展開表示中のタグ ID（未展開時は null）。</summary>
    public int? OpenTreeTagId { get; private set; }

    /// <summary>
    ///     現在ログイン中のユーザーがエッジを削除・管理可能かどうかを判定する。
    /// </summary>
    public bool CanManageEdge =>
        Edge != null &&
        !string.IsNullOrEmpty(CurrentUserId) &&
        Edge.OwnerId == CurrentUserId;

    /// <summary>
    ///     指定されたタグ紐付けを現在ログイン中のユーザーが解除可能かどうかを判定する。
    /// </summary>
    public bool CanManageAttachment(TagEdgeTagAttachment? attachment) =>
        attachment != null &&
        !string.IsNullOrEmpty(CurrentUserId) &&
        attachment.OwnerId == CurrentUserId;

    /// <summary>
    ///     指定されたタグのツリー表示トグル（開閉）を行います。
    /// </summary>
    public void ToggleTree(int tagId)
    {
        OpenTreeTagId = OpenTreeTagId == tagId ? null : tagId;
    }

    /// <summary>
    ///     開いているツリーを閉じます。
    /// </summary>
    public void CloseTree()
    {
        OpenTreeTagId = null;
    }
}