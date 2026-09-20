#region

using Microsoft.AspNetCore.Components;

using SRNSMudApp.Models;

#endregion

namespace SRNSMudApp.Components.Item;

/// <summary>
///     アイテム投稿時に、テキスト中のタグ名候補を検出し内部リンクへの変換をプレビューするUIコンポーネント。
///     自動置換（閾値以上）と手動確認候補の2グループに分類し、
///     ユーザーは「＋追加」や「×除外」で操作可能。閾値のスライダー調整にも対応する。
/// </summary>
public partial class LinkConversionPanel : ComponentBase
{
    /// <summary>
    ///     自動置換候補一覧（閾値以上のスコアを持つ候補）。
    /// </summary>
    [Parameter]
    public IReadOnlyList<LinkConversionCandidate> AutoReplaceCandidates { get; set; } = [];

    /// <summary>
    ///     手動確認候補一覧（閾値未満だが候補として検出された候補）。
    /// </summary>
    [Parameter]
    public IReadOnlyList<LinkConversionCandidate> ManualCandidates { get; set; } = [];

    /// <summary>
    ///     候補検出のローディング中フラグ。
    /// </summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>
    ///     現在の自動置換類似度閾値。
    /// </summary>
    [Parameter]
    public float AutoReplaceThreshold { get; set; } = LinkConversionCandidate.DefaultAutoReplaceThreshold;

    /// <summary>
    ///     閾値が変更された際の通知コールバック。
    /// </summary>
    [Parameter]
    public EventCallback<float> OnThresholdChanged { get; set; }

    protected bool ShowSettings { get; set; }

    protected void ToggleSettings()
    {
        ShowSettings = !ShowSettings;
    }

    protected async Task OnThresholdSliderChanged(float value)
    {
        AutoReplaceThreshold = LinkConversionPanelViewModel.ClampThreshold(value);
        await OnThresholdChanged.InvokeAsync(AutoReplaceThreshold);
    }

    protected async Task ResetThreshold()
    {
        AutoReplaceThreshold = LinkConversionCandidate.DefaultAutoReplaceThreshold;
        await OnThresholdChanged.InvokeAsync(AutoReplaceThreshold);
    }
}