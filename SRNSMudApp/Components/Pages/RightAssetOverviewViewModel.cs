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
public sealed class RightAssetOverviewViewModel
{
    private readonly IRightAssetDataProvider _dataProvider;
    private readonly IDialogLauncher _dialogLauncher;

    public RightAssetOverviewViewModel(
        IRightAssetDataProvider dataProvider,
        IDialogLauncher dialogLauncher)
    {
        _dataProvider = dataProvider;
        _dialogLauncher = dialogLauncher;
    }

    public Tag? SelectedTag { get; set; }
    public RightAssetOverviewData? OverviewData { get; private set; }
    public IReadOnlyList<TagRightAssetSummary> TopTags { get; private set; } = [];
    public bool IsLoading { get; private set; }
    public int? LoadedTagId { get; private set; }

    /// <summary>
    ///     初期化時に発行実績のある代表タグサマリーを取得する。
    /// </summary>
    public async Task InitializeAsync()
    {
        TopTags = await _dataProvider.GetTopTagsWithRightAssetsAsync(15);
    }

    /// <summary>
    ///     指定されたタグIDの RightAsset 保有状況データを読み込む。
    /// </summary>
    public async Task LoadDataByTagIdAsync(int tagId)
    {
        IsLoading = true;
        LoadedTagId = tagId;
        try
        {
            OverviewData = await _dataProvider.GetRightAssetOverviewByTagIdAsync(tagId);
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