namespace SRNSMudApp.Components.Pages;

using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using SRNSMudApp.Data;
using SRNSMudApp.Services;

/// <summary>
///     RightAsset 保有状況一覧画面コンポーネントのコードビハインド。
///     タグ検索・クエリパラメータ同期、URLナビゲーション、購入ダイアログ表示を制御する。
///     保有状況集計・データ取得ロジックは <see cref="RightAssetOverviewViewModel"/> に委譲する。
/// </summary>
public partial class RightAssetOverview : ComponentBase
{
    [Inject] private RightAssetOverviewViewModel ViewModel { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;

    /// <summary>
    ///     URL クエリ文字列 (?tagId=...) からバインドされる対象タグの ID。
    /// </summary>
    [SupplyParameterFromQuery]
    public int? TagId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await ViewModel.InitializeAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (TagId.HasValue && TagId.Value != ViewModel.LoadedTagId)
        {
            await ViewModel.LoadDataByTagIdAsync(TagId.Value);
        }
        else if (!TagId.HasValue && ViewModel.LoadedTagId.HasValue)
        {
            ViewModel.ClearSelection();
        }
    }

    private async Task OnTagSelectedAsync(Tag? tag)
    {
        if (tag != null)
        {
            NavigationManager.NavigateTo($"/RightAsset/Overview?tagId={tag.Id}", replace: false);
            await ViewModel.LoadDataByTagIdAsync(tag.Id);
        }
        else
        {
            NavigationManager.NavigateTo("/RightAsset/Overview", replace: false);
            ViewModel.ClearSelection();
        }
    }

    private async Task OnSelectTopTagAsync(int tagId)
    {
        NavigationManager.NavigateTo($"/RightAsset/Overview?tagId={tagId}", replace: false);
        await ViewModel.LoadDataByTagIdAsync(tagId);
    }

    private async Task OpenPurchaseDialogAsync()
    {
        await ViewModel.OpenPurchaseDialogAsync();
    }
}