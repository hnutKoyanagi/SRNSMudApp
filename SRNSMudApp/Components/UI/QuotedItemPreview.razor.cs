namespace SRNSMudApp.Components.UI;

using Microsoft.AspNetCore.Components;

using SRNSMudApp.Data;

/// <summary>
///     引用されたアイテムの要約プレビューを表示するコンポーネント。
///     データ取得およびナビゲーションURL計算は <see cref="QuotedItemPreviewViewModel"/> に委譲する。
/// </summary>
public partial class QuotedItemPreview : ComponentBase
{
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private QuotedItemPreviewViewModel ViewModel { get; set; } = null!;

    [Parameter] public int QuotedItemId { get; set; }
    [Parameter] public Item? QuotedItem { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        await ViewModel.UpdateParametersAsync(QuotedItemId, QuotedItem);
    }

    private void HandleClick()
    {
        if (ViewModel.NavigationUrl is not null)
        {
            NavigationManager.NavigateTo(ViewModel.NavigationUrl);
        }
    }
}