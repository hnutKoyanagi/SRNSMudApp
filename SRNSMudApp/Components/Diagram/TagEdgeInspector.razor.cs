namespace SRNSMudApp.Components.Diagram;

using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using SRNSMudApp.Data;
using SRNSMudApp.Services;

/// <summary>
///     タグエッジ詳細インスペクターコンポーネントのコードビハインド。
///     選択中エッジの始点/終点タグ、作成者情報、紐付けられた意味付けタグ一覧の表示、および削除・紐付け操作を制御する。
///     操作権限の判定やポップオーバーツリーの開閉状態は <see cref="TagEdgeInspectorViewModel"/> に委譲する。
/// </summary>
public partial class TagEdgeInspector : ComponentBase
{
    [Inject] private TagEdgeInspectorViewModel ViewModel { get; set; } = null!;

    /// <summary>
    ///     インスペクターで表示・操作の対象となるタグエッジ。
    /// </summary>
    [Parameter]
    public TagEdge? Edge { get; set; }

    /// <summary>
    ///     現在ログイン中のユーザーID。操作権限の判定に使用されます。
    /// </summary>
    [Parameter]
    public string CurrentUserId { get; set; } = "";

    /// <summary>
    ///     タグツリーなどで参照・選択可能な全タグの一覧。
    /// </summary>
    [Parameter]
    public IReadOnlyList<Tag> AllTags { get; set; } = [];

    /// <summary>
    ///     インスペクターを閉じる要求が発生した際のコールバック。
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>
    ///     エッジの削除が要求された際のコールバック。
    /// </summary>
    [Parameter]
    public EventCallback<TagEdge> OnDeleteEdge { get; set; }

    /// <summary>
    ///     意味付けタグの紐付けが要求された際のコールバック。
    /// </summary>
    [Parameter]
    public EventCallback<TagEdge> OnAttachTag { get; set; }

    /// <summary>
    ///     意味付けタグの紐付け解除が要求された際のコールバック。
    /// </summary>
    [Parameter]
    public EventCallback<TagEdgeTagAttachment> OnDetachTag { get; set; }

    /// <summary>
    ///     子タグの追加が要求された際のコールバック。
    /// </summary>
    [Parameter]
    public EventCallback<Tag> OnAddChildTag { get; set; }

    protected override void OnParametersSet()
    {
        ViewModel.Edge = Edge;
        ViewModel.CurrentUserId = CurrentUserId;
    }

    private async Task HandleAddChildTag(Tag targetTag)
    {
        ViewModel.CloseTree();
        if (OnAddChildTag.HasDelegate)
        {
            await OnAddChildTag.InvokeAsync(targetTag);
        }
    }
}