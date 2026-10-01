namespace SRNSMudApp.Components.Tag;

using System.Collections.Generic;

using Microsoft.AspNetCore.Components;

using SRNSMudApp.Data;

/// <summary>
///     アクションタグチップ表示コンポーネントのコードビハインド。
///     ItemTagChip をラップし、アクションタグ表示専用の設定を提供する。
/// </summary>
public partial class ActionTagChip : ComponentBase
{
    [Parameter]
    public TagRelation TagRelation { get; set; } = null!;

    [Parameter]
    public Item Item { get; set; } = null!;

    [Parameter]
    public string CurrentUserId { get; set; } = string.Empty;

    [Parameter]
    public IReadOnlyList<Tag> AllTags { get; set; } = [];

    [Parameter]
    public IReadOnlyList<TagRelationToTag> AllTagRelationsToTags { get; set; } = [];

    [Parameter]
    public TimelineEvent? HighlightEvent { get; set; }

    [Parameter]
    public EventCallback OnDataChanged { get; set; }

    [Parameter]
    public int ChipIndex { get; set; }

    [Parameter]
    public IReadOnlyList<string> ChipBackgrounds { get; set; } =
    [
        "#E8EAF6", "#E3F2FD", "#E0F7FA", "#E8F5E9", "#F3E5F5", "#FFF3E0", "#FFEBEE"
    ];

    [Parameter]
    public IReadOnlyList<string> ChipTextColors { get; set; } =
    [
        "#1A237E", "#0D47A1", "#006064", "#1B5E20", "#4A148C", "#E65100", "#B71C1C"
    ];
}