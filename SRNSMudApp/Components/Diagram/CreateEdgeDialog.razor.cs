namespace SRNSMudApp.Components.Diagram;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using TagEntity = SRNSMudApp.Data.Tag;

/// <summary>
///     ダイアグラム上のエッジ新規作成ダイアログコンポーネントのコードビハインド。
///     始点(Source)と終点(Target)タグのオートコンプリート検索、バリデーション、ダイアログ結果の返却を制御する。
/// </summary>
public partial class CreateEdgeDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public IReadOnlyList<TagEntity> AvailableTags { get; set; } = [];

    [Parameter]
    public TagEntity? InitialSourceTag { get; set; }

    [Parameter]
    public TagEntity? InitialTargetTag { get; set; }

    private TagEntity? _sourceTag;
    private TagEntity? _targetTag;

    protected override void OnInitialized()
    {
        if (InitialSourceTag != null)
        {
            _sourceTag = InitialSourceTag;
        }

        if (InitialTargetTag != null)
        {
            _targetTag = InitialTargetTag;
        }
    }

    private bool CanSubmit =>
        _sourceTag != null &&
        _targetTag != null &&
        _sourceTag.Id != _targetTag.Id;

    private Task<IEnumerable<TagEntity>> SearchSourceTags(string value, CancellationToken token) =>
        SearchTagsInternal(value, _targetTag?.Id);

    private Task<IEnumerable<TagEntity>> SearchTargetTags(string value, CancellationToken token) =>
        SearchTagsInternal(value, _sourceTag?.Id);

    private Task<IEnumerable<TagEntity>> SearchTagsInternal(string value, int? excludeId)
    {
        IEnumerable<TagEntity> query = AvailableTags;
        if (excludeId.HasValue)
        {
            query = query.Where(t => t.Id != excludeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            query = query.Where(t => t.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(query.Take(20));
    }

    private void Cancel() => MudDialog.Cancel();

    private void Submit()
    {
        if (CanSubmit)
        {
            MudDialog.Close(DialogResult.Ok((SourceTagId: _sourceTag!.Id, TargetTagId: _targetTag!.Id)));
        }
    }
}