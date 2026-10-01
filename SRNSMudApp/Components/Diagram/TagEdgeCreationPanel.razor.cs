namespace SRNSMudApp.Components.Diagram;

using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Components;

using SRNSMudApp.Data;

using TagEntity = SRNSMudApp.Data.Tag;

/// <summary>
///     タグダイアグラムにおけるエッジ作成パネルコンポーネントのコードビハインド。
///     始点(From)・終点(To)タグの選択、意味付けタグと付与Weightの指定、子タグクイック設定を制御する。
/// </summary>
public partial class TagEdgeCreationPanel : ComponentBase
{
    [Parameter] public bool IsEdgeCreationMode { get; set; }
    [Parameter] public TagEntity? FocusedTag { get; set; }
    [Parameter] public IReadOnlyList<TagEntity> AllTags { get; set; } = [];
    [Parameter] public TagEntity? SourceTag { get; set; }
    [Parameter] public TagEntity? TargetTag { get; set; }
    [Parameter] public TagEntity? AttachTag { get; set; }
    [Parameter] public RightAsset? AttachAsset { get; set; }
    [Parameter] public IReadOnlyList<RightAsset> AvailableAssets { get; set; } = [];
    [Parameter] public int Weight { get; set; } = 1;
    [Parameter] public bool IsLoadingAttachAssets { get; set; }
    [Parameter] public bool CanCreateEdge { get; set; }

    [Parameter] public EventCallback OnCreateEdge { get; set; }
    [Parameter] public EventCallback OnExitMode { get; set; }
    [Parameter] public EventCallback<TagEntity?> OnSourceTagChanged { get; set; }
    [Parameter] public EventCallback<TagEntity?> OnTargetTagChanged { get; set; }
    [Parameter] public EventCallback<TagEntity?> OnAttachTagChanged { get; set; }
    [Parameter] public EventCallback<int> OnWeightChanged { get; set; }
    [Parameter] public EventCallback OnSwapTags { get; set; }
    [Parameter] public EventCallback<TagEntity> OnSelectChildAsSource { get; set; }
    [Parameter] public EventCallback<TagEntity> OnSelectChildAsTarget { get; set; }

    private List<TagEntity> GetChildTags(int parentTagId) =>
        AllTags.Where(t => t.ParentTagId == parentTagId).OrderBy(t => t.Name).ToList();
}