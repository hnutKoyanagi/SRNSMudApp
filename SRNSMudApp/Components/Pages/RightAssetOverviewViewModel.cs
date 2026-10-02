namespace SRNSMudApp.Components.Pages;

using MudBlazor;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;

/// <summary>
///     RightAssetOverview 画面の状態保持およびデータフェッチ・操作を担う ViewModel。
/// </summary>
public sealed class RightAssetOverviewViewModel(
    IRightAssetDataProvider dataProvider,
    IDialogLauncher dialogLauncher)
{
    private readonly IRightAssetDataProvider _dataProvider =
        dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
    private readonly IDialogLauncher _dialogLauncher =
        dialogLauncher ?? throw new ArgumentNullException(nameof(dialogLauncher));

    /// <summary>
    ///     現在選択中のタグ。
    /// </summary>
    public Tag? SelectedTag { get; private set; }

    /// <summary>
    ///     選択中タグの RightAsset 保有状況データ。
    /// </summary>
    public RightAssetOverviewData? OverviewData { get; private set; }

    /// <summary>
    ///     発行実績のある代表的なタグサマリー一覧。
    /// </summary>
    public IReadOnlyList<TagRightAssetSummary> TopTags { get; private set; } = [];

    /// <summary>
    ///     データ読み込み中かどうかを示す値。
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    ///     現在読み込み完了しているタグの ID。
    /// </summary>
    public int? LoadedTagId { get; private set; }

    /// <summary>
    ///     初期化時に発行実績のある代表タグサマリーを取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセレーショントークン。</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        TopTags = await _dataProvider.GetTopTagsWithRightAssetsAsync(15, cancellationToken);
    }

    /// <summary>
    ///     指定されたタグIDの RightAsset 保有状況データを読み込む。
    /// </summary>
    /// <param name="tagId">読み込み対象のタグID。</param>
    /// <param name="cancellationToken">キャンセレーショントークン。</param>
    public async Task LoadDataByTagIdAsync(int tagId, CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        LoadedTagId = tagId;
        try
        {
            OverviewData = await _dataProvider.GetRightAssetOverviewByTagIdAsync(tagId, cancellationToken);
            if (OverviewData != null)
            {
                SelectedTag = OverviewData.Tag;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    ///     選択されたタグをクリアする。
    /// </summary>
    public void ClearSelection()
    {
        SelectedTag = null;
        OverviewData = null;
        LoadedTagId = null;
    }

    /// <summary>
    ///     保有シェア（パーセンテージ）を計算する。
    /// </summary>
    /// <param name="userAmount">ユーザーが保有する数量。</param>
    /// <param name="totalActiveAmount">アクティブな総発行数量。</param>
    /// <returns>総数量に対する保有シェアの割合（0.0 〜 100.0）。</returns>
    public static double CalculateShare(int userAmount, int totalActiveAmount)
    {
        if (totalActiveAmount <= 0)
        {
            return 0.0;
        }

        return (double)userAmount / totalActiveAmount * 100.0;
    }

    /// <summary>
    ///     JPYC による RightAsset 購入ダイアログを表示し、成功時はデータを再読み込みする。
    /// </summary>
    /// <returns>ダイアログで正常に購入が行われ、データが再読み込みされた場合は <c>true</c>。それ以外は <c>false</c>。</returns>
    public async Task<bool> OpenPurchaseDialogAsync()
    {
        if (OverviewData?.Tag is null)
        {
            return false;
        }

        var parameters = new DialogParameters<PurchaseRightAssetDialog>
        {
            { x => x.RequestedTag, OverviewData.Tag }
        };
        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await _dialogLauncher.ShowAsync<PurchaseRightAssetDialog>("操作権限の購入 (JPYC)", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false } && LoadedTagId.HasValue)
        {
            await LoadDataByTagIdAsync(LoadedTagId.Value);
            return true;
        }

        return false;
    }
}