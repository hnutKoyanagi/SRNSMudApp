namespace SRNSMudApp.Components.PublicOffer;

using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services.Dialogs;

/// <summary>
///     公開オファー掲示板画面コンポーネントのコードビハインド。
///     公開オファー一覧の表示、新規作成ダイアログの起動、オファーの受託および取り下げを制御する。
/// </summary>
public partial class PublicOfferBoard : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    [Inject]
    private PublicOfferBoardViewModel ViewModel { get; set; } = null!;

    [Inject]
    private IDialogLauncher DialogLauncher { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask;
        ViewModel.CurrentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        await ViewModel.LoadDataAsync();
    }

    private async Task OpenCreateDialog()
    {
        var options = PublicOfferBoardViewModel.CreateDialogOptions();
        var dialog = await DialogLauncher.ShowAsync<CreatePublicOfferDialog>("公開オファーの作成", options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await ViewModel.LoadDataAsync();
        }
    }

    private async Task TriggerOffer(PublicTradeOffer offer)
    {
        var parameters = PublicOfferBoardViewModel.TriggerDialogParameters(offer);
        var options = PublicOfferBoardViewModel.TriggerDialogOptions();
        var dialog = await DialogLauncher.ShowAsync<TriggerPublicOfferDialog>("オファーに応じる", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: int contractId })
        {
            var acceptResult = await ViewModel.AcceptTriggeredContractAsync(contractId);
            switch (acceptResult)
            {
                case Success<bool>:
                    Snackbar.Add("公開オファーを利用してタグを獲得しました！", Severity.Success);
                    break;
                case Failure f:
                    Snackbar.Add(f.ErrorMessage, Severity.Error);
                    break;
                default:
                    break;
            }
        }
    }

    private async Task DeactivateOffer(PublicTradeOffer offer)
    {
        var result = await ViewModel.DeactivateOfferAsync(offer);
        switch (result)
        {
            case Success<bool>:
                Snackbar.Add("オファーを取り下げました。", Severity.Success);
                break;
            case Failure f:
                Snackbar.Add(f.ErrorMessage, Severity.Error);
                break;
            default:
                break;
        }
    }
}