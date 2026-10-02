namespace SRNSMudApp.Components.Item;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using SRNSMudApp.Data;

/// <summary>
///     ソート条件エディタコンポーネントのコードビハインド。
///     ソート対象タグ追加、昇順・降順切り替え、条件削除、JSONエクスポートのイベント連携を制御する。
/// </summary>
public partial class SortConditionEditor : ComponentBase
{
    [Parameter]
    public IReadOnlyList<SortCondition> SortConditions { get; set; } = [];

    [Parameter]
    public EventCallback<Tag?> OnSortTargetTagAdded { get; set; }

    [Parameter]
    public Func<string?, CancellationToken, Task<IEnumerable<Tag>>> SearchSortTags { get; set; } =
        (_, _) => Task.FromResult<IEnumerable<Tag>>([]);

    [Parameter]
    public EventCallback<SortCondition> OnToggleOrder { get; set; }

    [Parameter]
    public EventCallback<SortCondition> OnRemoveCondition { get; set; }

    [Parameter]
    public EventCallback OnExport { get; set; }
}