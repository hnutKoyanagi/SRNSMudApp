namespace SRNSMudApp.Components.UI;

using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using SRNSMudApp.Components.PublicOffer;
using SRNSMudApp.Data;
using SRNSMudApp.Services.Dialogs;

/// <summary>
///     パブリックオファー一覧テーブルコンポーネントのコードビハインド。
///     オファー一覧の表示およびオファー受諾（実行）ダイアログの起動を制御する。
/// </summary>
public partial class PublicTradeOfferList : ComponentBase
{
    [Parameter]
    public IEnumerable<PublicTradeOffer> Offers { get; set; } = [];

    [Parameter]
    public EventCallback OnOfferTriggered { get; set; }

    [Inject]
    private IDialogLauncher DialogLauncher { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    private async Task TriggerOffer(PublicTradeOffer offer)
    {
        var parameters = new DialogParameters
        {
            { "Offer", offer }
        };

        var dialog = await DialogLauncher.ShowAsync<TriggerPublicOfferDialog>("オファーを受諾", parameters);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add("オファーを受諾し、リクエストを作成しました。", Severity.Success);
            await OnOfferTriggered.InvokeAsync();
        }
    }
}